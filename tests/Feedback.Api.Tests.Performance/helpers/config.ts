import { RefinedResponse, ResponseType } from "k6/http";

export const BASE_URL: string = __ENV.BASE_URL || "http://localhost:5067";

export const ENDPOINTS = {
  events: `${BASE_URL}/api/events`,
  eventById: (id: number) => `${BASE_URL}/api/events/${id}`,
  eventTickets: (id: number) => `${BASE_URL}/api/events/${id}/tickets`,
  eventAttendees: (id: number) => `${BASE_URL}/api/events/${id}/attendees`,
  ticketByCode: (code: string) => `${BASE_URL}/api/tickets/${code}`,
  useTicket: (code: string) => `${BASE_URL}/api/tickets/${code}/use`,
} as const;

export const HEADERS: Record<string, string> = {
  "Content-Type": "application/json",
};

export const THRESHOLDS: Record<string, string[]> = {
  http_req_duration: ["p(95)<500", "p(99)<1000"],
  http_req_failed: ["rate<0.01"],
};

export interface CreateEventRequest {
  title: string;
  description: string;
  venueId: number;
  date: string;        // ISO date YYYY-MM-DD
  startTime: string;   // HH:mm:ss
  endTime: string;
  totalTickets: number;
  ticketPrice: number;
}

export interface PurchaseTicketsRequest {
  buyerName: string;
  buyerEmail: string;
  quantity: number;
}

export interface EventResponse {
  id: number;
  title: string;
  availableTickets: number;
  ticketPrice: number;
}

export interface TicketResponse {
  id: number;
  ticketCode: string;
  isUsed: boolean;
}

export function parseBody<T>(res: RefinedResponse<ResponseType>): T {
  return JSON.parse(res.body as string) as T;
}

export function randomEmail(prefix: string): string {
  return `${prefix}-${__VU}-${__ITER}@perf-test.com`;
}

// Seeded venue IDs available in the performance test DB
export const SEEDED_VENUE_IDS: number[] = Array.from({ length: 20 }, (_, i) => i + 1);
export const SEEDED_EVENT_IDS: number[] = Array.from({ length: 500 }, (_, i) => i + 1);

