// Smoke test: 1 VU, 30s — verify API is alive and basic CRUD works
import { check, sleep } from "k6";
import { Options } from "k6/options";
import { THRESHOLDS, EventResponse, TicketResponse, parseBody, randomEmail } from "../helpers/config.ts";
import {
  getEvents,
  createEvent,
  getEventById,
  purchaseTickets,
  getTicketByCode,
  useTicket,
} from "../helpers/api-client.ts";

export const options: Options = {
  vus: 1,
  duration: "30s",
  thresholds: THRESHOLDS,
};

// A venue ID that must exist in the seeded DB (ID=1)
const VENUE_ID = 1;

export default function () {
  // List events
  const listRes = getEvents();
  check(listRes, { "GET /api/events → 200": (r) => r.status === 200 });

  // Create an event
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 30);
  const dateStr = tomorrow.toISOString().split("T")[0];

  const createRes = createEvent({
    title: `Smoke Test Event ${__VU}-${__ITER}`,
    description: "Performance smoke test event",
    venueId: VENUE_ID,
    date: dateStr,
    startTime: "18:00:00",
    endTime: "22:00:00",
    totalTickets: 10,
    ticketPrice: 19.99,
  });
  check(createRes, { "POST /api/events → 201": (r) => r.status === 201 });

  if (createRes.status === 201) {
    const ev = parseBody<EventResponse>(createRes);

    // Get detail
    const detailRes = getEventById(ev.id);
    check(detailRes, { "GET /api/events/{id} → 200": (r) => r.status === 200 });

    // Purchase tickets
    const purchaseRes = purchaseTickets(ev.id, {
      buyerName: "Smoke Tester",
      buyerEmail: randomEmail("smoke-buyer"),
      quantity: 2,
    });
    check(purchaseRes, { "POST /api/events/{id}/tickets → 201": (r) => r.status === 201 });

    if (purchaseRes.status === 201) {
      const tickets = parseBody<TicketResponse[]>(purchaseRes);
      const code = tickets[0].ticketCode;

      // Verify ticket
      const verifyRes = getTicketByCode(code);
      check(verifyRes, { "GET /api/tickets/{code} → 200": (r) => r.status === 200 });

      // Use ticket (scan at entry)
      const useRes = useTicket(code);
      check(useRes, { "PATCH /api/tickets/{code}/use → 200": (r) => r.status === 200 });

      // Prevent double use
      const doubleUse = useTicket(code);
      check(doubleUse, { "PATCH ticket second use → 409": (r) => r.status === 409 });
    }
  }

  sleep(1);
}
