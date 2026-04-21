// Stress test: Push to 100 VUs — flash sale: concurrent ticket purchases on a single event
import { check, sleep } from "k6";
import { Options } from "k6/options";
import { TicketResponse, parseBody, randomEmail, SEEDED_EVENT_IDS } from "../helpers/config.ts";
import {
  getEvents,
  getEventById,
  purchaseTickets,
  useTicket,
} from "../helpers/api-client.ts";

export const options: Options = {
  stages: [
    { duration: "1m", target: 10 },
    { duration: "2m", target: 10 },
    { duration: "1m", target: 50 },
    { duration: "2m", target: 50 },
    { duration: "1m", target: 100 },
    { duration: "2m", target: 100 },
    { duration: "2m", target: 0 },
  ],
  thresholds: {
    http_req_duration: ["p(95)<1000", "p(99)<2000"],
    http_req_failed: ["rate<0.05"],
  },
};

export default function () {
  // Stress concurrent ticket purchase — simulate flash sale
  const eventId = SEEDED_EVENT_IDS[__VU % SEEDED_EVENT_IDS.length];

  const purchaseRes = purchaseTickets(eventId, {
    buyerName: `Stress Buyer ${__VU}`,
    buyerEmail: randomEmail(`stress-buyer-${__VU}`),
    quantity: 1,
  });

  check(purchaseRes, {
    "POST tickets (stress) → 201 or 409 or 404": (r) =>
      r.status === 201 || r.status === 409 || r.status === 404,
  });

  if (purchaseRes.status === 201) {
    const tickets = parseBody<TicketResponse[]>(purchaseRes);
    const code = tickets[0].ticketCode;

    // Simulate scanning at entry
    const useRes = useTicket(code);
    check(useRes, {
      "PATCH tickets/{code}/use (stress) → 200 or 409": (r) =>
        r.status === 200 || r.status === 409,
    });
  }

  // Also load the event list under stress
  const listRes = getEvents();
  check(listRes, { "GET events (stress) → 200": (r) => r.status === 200 });

  sleep(0.3);
}
