/**
 * What a refusal says on screen.
 *
 * The strings live here rather than in the export's `T`, because adding to `T` means editing
 * two hundred lines of somebody else's translation table inside a seam. They are still keyed by
 * the same `lang`, so the app has one notion of what language it is in.
 *
 * The four refusals are kept apart on purpose. 401, 403 and 404 mean sign in, you may not, and
 * it is not there — telling them apart is the difference between a usable screen and one that
 * says "something went wrong" to all three.
 */

import { ApiError } from "../api/client";

export type Lang = "en" | "zh";

export interface FailureText {
  title: string;
  detail: string;
}

interface Messages {
  loading: string;
  retry: string;
  signOut: string;
  /** 401: the request needs a caller and there is not one. */
  unauthenticated: FailureText;
  /** 403: there is a caller, and this is not theirs to do. */
  forbidden: FailureText;
  /** 404: unknown, deleted, or not readable — the API does not say which. */
  notFound: FailureText;
  /** Status 0: the request never reached the server. */
  unreachable: FailureText;
  failed: FailureText;
}

const MESSAGES: Record<Lang, Messages> = {
  en: {
    loading: "Loading…",
    retry: "Try again",
    signOut: "Sign out",
    unauthenticated: {
      title: "Sign in to continue",
      detail: "This needs an account. Sign in with Microsoft and try again.",
    },
    forbidden: {
      title: "Not allowed",
      detail: "You are signed in, but this is not yours to change.",
    },
    notFound: {
      title: "Not found",
      detail: "It is not here — it may have been removed, or it may never have been shared with you.",
    },
    unreachable: {
      title: "Cannot reach the server",
      detail: "The request did not get an answer. Check your connection and try again.",
    },
    failed: {
      title: "Something went wrong",
      detail: "The request was refused.",
    },
  },
  zh: {
    loading: "加载中……",
    retry: "重试",
    signOut: "退出登录",
    unauthenticated: {
      title: "请先登录",
      detail: "此操作需要账户。请使用 Microsoft 登录后重试。",
    },
    forbidden: {
      title: "无权操作",
      detail: "您已登录，但无权更改此内容。",
    },
    notFound: {
      title: "未找到",
      detail: "内容不存在——可能已被删除，或从未共享给您。",
    },
    unreachable: {
      title: "无法连接服务器",
      detail: "请求未获得响应。请检查网络后重试。",
    },
    failed: {
      title: "出错了",
      detail: "请求被拒绝。",
    },
  },
};

export function failureText(lang: Lang, error: ApiError): FailureText {
  const messages = MESSAGES[lang];
  switch (error.status) {
    case 401:
      return messages.unauthenticated;
    case 403:
      return messages.forbidden;
    case 404:
      return messages.notFound;
    case 0:
      return messages.unreachable;
    default:
      return { ...messages.failed, detail: error.message || messages.failed.detail };
  }
}

export function loadingText(lang: Lang): string {
  return MESSAGES[lang].loading;
}

export function retryText(lang: Lang): string {
  return MESSAGES[lang].retry;
}

export function signOutText(lang: Lang): string {
  return MESSAGES[lang].signOut;
}

/** A field's own message from a `ValidationProblemDetails`, or null when the server named none. */
export function fieldError(error: ApiError | null, field: string): string | null {
  return error?.fieldError(field) ?? null;
}
