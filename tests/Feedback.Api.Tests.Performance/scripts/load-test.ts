// Load test: Ramp to 20 VUs — test event listing under sustained load
import { check, sleep } from "k6";
import { Options } from "k6/options";
import { THRESHOLDS, EventResponse, TicketResponse, parseBody, randomEmail, SEEDED_EVENT_IDS } from "../helpers/config.ts";
import {
  getEvents,
  getEventById,
  purchaseTickets,
  getAttendees,
} from "../helpers/api-client.ts";

export const options: Options = {
  stages: [
    { duration: "1m", target: 10 },
    { duration: "3m", target: 20 },
    { duration: "1m", target: 0 },
  ],
  thresholds: THRESHOLDS,
};

export default function () {
  // List all upcoming events (most common read path)
  const listRes = getEvents();
  check(listRes, { "GET /api/events → 200": (r) => r.status === 200 });

  // Filter by venue
  const filteredRes = getEvents((__VU % 20) + 1);
  check(filteredRes, { "GET /api/events?venueId → 200": (r) => r.status === 200 });

  // Get a specific event detail
  const eventId = SEEDED_EVENT_IDS[__ITER % SEEDED_EVENT_IDS.length];
  const detailRes = getEventById(eventId);
  check(detailRes, { "GET /api/events/{id} → 200 or 404": (r) => r.status === 200 || r.status === 404 });

  if (detailRes.status === 200) {
    const ev = parseBody<EventResponse>(detailRes);
    if (ev.availableTickets > 0) {
      const purchaseRes = purchaseTickets(ev.id, {
        buyerName: `VU ${__VU}`,
        buyerEmail: randomEmail(`load-buyer-${__VU}`),
        quantity: 1,
      });
      check(purchaseRes, { "POST /api/events/{id}/tickets → 201 or 409": (r) => r.status === 201 || r.status === 409 });
    }

    const attendeesRes = getAttendees(ev.id);
    check(attendeesRes, { "GET /api/events/{id}/attendees → 200 or 404": (r) => r.status === 200 || r.status === 404 });
  }

  sleep(0.5);
}
