import {
  BrowserUtils,
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
} from "@azure/msal-browser";
import { broadcastResponseToMainFrame } from "@azure/msal-browser/redirect-bridge";
import { useSyncExternalStore } from "react";

import type { WireProfile } from "../api/types";
import { getProfile } from "../api/endpoints";
import { setTokenSource } from "../api/client";
import { entra } from "../config";
import { forgetAll } from "../data/mediaUrlCache";

export type AuthRole = "visitor" | "user" | "admin";

export interface AuthState {
  status: "unconfigured" | "initialising" | "visitor" | "signed-in";
  role: AuthRole;
  userId: string;
  displayName: string;
  avatarUrl: string | null;
  error: string | null;
}

const msal: PublicClientApplication | null = entra
  ? new PublicClientApplication({
      auth: {
        clientId: entra.clientId,
        authority: `https://login.microsoftonline.com/${entra.tenantId}`,
        redirectUri: window.location.origin,
      },
      cache: { cacheLocation: "sessionStorage" },
    })
  : null;

export const signInAvailable: boolean = entra !== null;

const ANONYMOUS: AuthState = {
  status: entra ? "initialising" : "unconfigured",
  role: "visitor",
  userId: "",
  displayName: "",
  avatarUrl: null,
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

async function acquireToken(account: AccountInfo): Promise<string | null> {
  if (!msal || !entra) return null;
  const request = { scopes: entra.scopes, account };
  try {
    return (await msal.acquireTokenSilent(request)).idToken;
  } catch (silentFailure) {
    if (!(silentFailure instanceof InteractionRequiredAuthError)) throw silentFailure;
    return (await msal.acquireTokenPopup(request)).accessToken;
  }
}

async function adopt(account: AccountInfo): Promise<void> {
  setTokenSource(() => acquireToken(account));
  setState({
    status: "signed-in",
    role: "user",
    userId: "",
    displayName: account.name ?? "",
    avatarUrl: null,
    error: null,
  });

  try {
    applyProfile(await getProfile(), account.name ?? "");
  } catch (failure) {
    setState({ error: failure instanceof Error ? failure.message : String(failure) });
  }
}

export function applyProfile(profile: WireProfile, fallbackName = ""): void {
  setState({
    role: profile.role === "Admin" ? "admin" : "user",
    userId: profile.id,
    displayName: profile.displayName ?? fallbackName,
    avatarUrl: profile.avatarUrl ?? null,
  });
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

  // A media URL is a bearer token too, and it was minted for the session that just ended. Leaving
  // it held would let a signed-out browser keep rendering private bytes from its own cache.
  forgetAll();

  state = ANONYMOUS;
  setState({});
}

function hasAuthResponse(): boolean {
  try {
    BrowserUtils.parseAuthResponseFromUrl();
    return true;
  } catch {
    return false;
  }
}

async function initialise(): Promise<void> {
  if (!msal || !entra) return;
  try {
    if (hasAuthResponse()) {
      await broadcastResponseToMainFrame();
      return;
    }

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
