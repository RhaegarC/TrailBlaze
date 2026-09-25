/**
 * The one place a request is made, so the API's status codes are read once rather than at each
 * call site.
 */

import { apiBaseUrl } from "../config";

/** RFC 7807, in the shape `[ApiController]` produces. */
export interface Problem {
  title?: string;
  status?: number;
  detail?: string;
  /** Field-keyed messages, which is how every validation rejection arrives. */
  errors?: Record<string, string[]>;
}

/**
 * A refusal, with the status kept rather than flattened into a message.
 *
 * 401, 403 and 404 mean three different things to the person looking at the screen — sign in,
 * you may not, and it is not there — and the app is required to keep them apart rather than
 * showing one "something went wrong" for all three.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: Problem | null;

  constructor(status: number, problem: Problem | null, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  /** The first message the server gave for `field`, or null when it named none. */
  fieldError(field: string): string | null {
    return this.problem?.errors?.[field]?.[0] ?? null;
  }
}

/**
 * Anything thrown by a request, as an `ApiError`.
 *
 * Status 0 is a transport failure — the request never reached the server — which is worth
 * keeping apart from a refusal, and is why it is not folded into 500.
 */
export function toApiError(failure: unknown): ApiError {
  if (failure instanceof ApiError) return failure;
  return new ApiError(0, null, failure instanceof Error ? failure.message : String(failure));
}

/** The problem's own words if it has any, the bare status otherwise. */
function describe(status: number, problem: Problem | null): string {
  const first = problem?.errors && Object.values(problem.errors)[0]?.[0];
  return first ?? problem?.detail ?? problem?.title ?? `The request failed (${status}).`;
}

type TokenSource = () => Promise<string | null>;

// Registered by `auth/store.ts` on import. A getter rather than an argument on every call,
// because the screens that trigger a request have no token to pass and should not need one.
let tokenSource: TokenSource = async () => null;

export function setTokenSource(source: TokenSource): void {
  tokenSource = source;
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  /** Multipart. Passed through untouched; the browser sets the boundary. */
  form?: FormData;
  signal?: AbortSignal;
  /** Send no bearer token even when one is available — the two anonymous routes. */
  anonymous?: boolean;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = "GET", body, form, signal, anonymous = false } = options;
  const headers: Record<string, string> = { Accept: "application/json" };

  if (!anonymous) {
    const token = await tokenSource();
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  if (body !== undefined) headers["Content-Type"] = "application/json";

  const response = await fetch(`${apiBaseUrl}${path}`, {
    method,
    headers,
    body: form ?? (body !== undefined ? JSON.stringify(body) : undefined),
    signal,
  });

  if (response.ok) {
    // 204 has no body, and `response.json()` on one throws.
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
  }

  const problem = await readProblem(response);
  throw new ApiError(response.status, problem, describe(response.status, problem));
}

/**
 * The refusal's body, when it has one.
 *
 * A `ValidationProblemDetails` and the bare `"No file was sent."` string are both possible, and
 * neither is guaranteed — an empty 403 or 404 is normal here — so this never throws on its own.
 */
async function readProblem(response: Response): Promise<Problem | null> {
  try {
    const text = await response.text();
    if (!text) return null;
    const parsed: unknown = JSON.parse(text);
    if (typeof parsed === "string") return { detail: parsed };
    return typeof parsed === "object" && parsed !== null ? (parsed as Problem) : null;
  } catch {
    return null;
  }
}
