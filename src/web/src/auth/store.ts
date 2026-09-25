/**
 * Who is signed in, as a store rather than a context.
 *
 * MSAL is already a page-wide singleton, and the screen that renders the header is the same one
 * that decides the whole view, so a provider would have to wrap the component that needs it.
 * A store read through `useSyncExternalStore` avoids that and keeps the sign-in state in the
 * one place the token comes from.
 */

import {
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
} from "@azure/msal-browser";
import { useSyncExternalStore } from "react";

import { getProfile } from "../api/endpoints";
import { setTokenSource } from "../api/client";
import { entra } from "../config";

export type AuthRole = "visitor" | "user" | "admin";

export interface AuthState {
  /** `unconfigured` is a deployment with no tenant: the app runs anonymously and cannot sign in. */
  status: "unconfigured" | "initialising" | "visitor" | "signed-in";
  role: AuthRole;
  /** The Entra object id, and therefore what `createdByUserId` on an activity compares against. */
  userId: string;
  displayName: string;
  /** Set when the last sign-in attempt failed, so the header can say so rather than nothing. */
  error: string | null;
}

const msal: PublicClientApplication | null = entra
  ? new PublicClientApplication({
      auth: {
        clientId: entra.clientId,
        authority: `https://login.microsoftonline.com/${entra.tenantId}`,
        // The SPA redirect URI has to be registered against the app registration; this is the
        // origin the app is actually served from, which differs per environment by construction.
        redirectUri: window.location.origin,
      },
      cache: { cacheLocation: "sessionStorage" },
    })
  : null;

/** False in a deployment with no tenant, where the sign-in link has nothing to open. */
export const signInAvailable: boolean = entra !== null;

const ANONYMOUS: AuthState = {
  status: entra ? "initialising" : "unconfigured",
  role: "visitor",
  userId: "",
  displayName: "",
  error: null,
};

let state: AuthState = ANONYMOUS;
const listeners = new Set<() => void>();

function setState(next: Partial<AuthState>): void {
  state = { ...state, ...next };
  for (const listener of listeners) listener();
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getAuthState(): AuthState {
  return state;
}

export function useAuth(): AuthState {
  return useSyncExternalStore(subscribe, getAuthState);
}

/** The account's token, or null when nobody is signed in or the tenant is not configured. */
async function acquireToken(account: AccountInfo): Promise<string | null> {
  if (!msal || !entra) return null;
  const request = { scopes: entra.scopes, account };
  try {
    return (await msal.acquireTokenSilent(request)).accessToken;
  } catch (silentFailure) {
    // An expired refresh token is the ordinary case here, and it is answered by asking the
    // person rather than by failing the request. Anything else is a real failure and is
    // reported as one by the caller that needed the token.
    if (!(silentFailure instanceof InteractionRequiredAuthError)) throw silentFailure;
    return (await msal.acquireTokenPopup(request)).accessToken;
  }
}

/**
 * Adopt a signed-in account: wire its tokens into the client, then read the role from the
 * server.
 *
 * The role is deliberately the server's to state rather than the token's to claim. Until
 * `GET /user/me` answers, the caller is an ordinary user — the narrower of the two, so an
 * admin affordance that has not arrived yet is late rather than shown to someone who may not
 * have it.
 */
async function adopt(account: AccountInfo): Promise<void> {
  setTokenSource(() => acquireToken(account));
  setState({
    status: "signed-in",
    role: "user",
    userId: "",
    displayName: account.name ?? "",
    error: null,
  });

  try {
    const profile = await getProfile();
    setState({
      role: profile.role === "Admin" ? "admin" : "user",
      // The API's own id for this caller, which is what an activity's `createdByUserId` holds.
      userId: profile.id,
      displayName: profile.displayName ?? account.name ?? "",
    });
  } catch (failure) {
    // Signed in but unidentifiable, which the API answers 401 for. Keeping `visitor`-level
    // affordances while holding a token is the safe half of that: nothing is offered that the
    // server has not just confirmed.
    setState({ error: failure instanceof Error ? failure.message : String(failure) });
  }
}

export async function signIn(): Promise<void> {
  if (!msal || !entra) return;
  try {
    const result = await msal.loginPopup({ scopes: entra.scopes });
    await adopt(result.account);
  } catch (failure) {
    setState({ error: failure instanceof Error ? failure.message : String(failure) });
  }
}

export async function signOut(): Promise<void> {
  if (!msal) return;
  const account = msal.getAllAccounts()[0];
  try {
    if (account) await msal.logoutPopup({ account });
  } catch {
    // A dismissed popup still means the intent was to leave; the local state is cleared either
    // way, so the token stops being attached to requests.
  }
  setTokenSource(async () => null);
  state = ANONYMOUS;
  setState({});
}

/** Runs once, at import. Restores an account from the MSAL cache so a reload stays signed in. */
async function initialise(): Promise<void> {
  if (!msal || !entra) return;
  try {
    await msal.initialize();
    const account = msal.getAllAccounts()[0] ?? null;
    if (account) await adopt(account);
    else setState({ status: "visitor" });
  } catch (failure) {
    setState({
      status: "visitor",
      error: failure instanceof Error ? failure.message : String(failure),
    });
  }
}

void initialise();
