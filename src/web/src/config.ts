/**
 * Every environment-specific value the app needs, read once at module load.
 *
 * Nothing is defaulted. A missing key stops the app and names the key, which is the API's own
 * `RequireSetting` habit: an origin that silently falls back to localhost is an origin nobody
 * chose, and it fails as a network error rather than as the missing setting it is.
 */

declare global {
  interface ImportMetaEnv {
    readonly VITE_API_BASE_URL?: string;
    readonly VITE_ENTRA_CLIENT_ID?: string;
    readonly VITE_ENTRA_TENANT_ID?: string;
    readonly VITE_ENTRA_SCOPE?: string;
  }
}

/** Sign-in, once a tenant is configured. */
export interface EntraConfig {
  clientId: string;
  tenantId: string;
  scopes: string[];
}

function required(name: string, value: string | undefined): string {
  const trimmed = value?.trim();
  if (!trimmed) {
    throw new Error(
      `${name} is not set. Copy .env.example to .env and fill it in, or set it where the app is built.`
    );
  }
  return trimmed;
}

// No trailing slash: every route below starts with one, and `${base}/${path}` would double it.
export const apiBaseUrl = required(
  "VITE_API_BASE_URL",
  import.meta.env.VITE_API_BASE_URL
).replace(/\/+$/, "");

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID?.trim();
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID?.trim();

/**
 * Sign-in configuration, or null when this deployment has no tenant.
 *
 * Both or neither, as on the API side, and blank is a working deployment rather than a broken
 * one: anonymous browsing is the public list's whole audience, and it is the only mode
 * available before an Entra tenant exists.
 */
export const entra: EntraConfig | null =
  clientId && tenantId
    ? {
        clientId,
        tenantId,
        // `<clientId>/.default`, not `api://<clientId>/.default`: the app registration is its own
        // audience and declares no identifier URI, so the `api://` form names a principal that
        // does not exist (AADSTS500011).
        scopes: [import.meta.env.VITE_ENTRA_SCOPE?.trim() || `${clientId}/.default`],
      }
    : null;
