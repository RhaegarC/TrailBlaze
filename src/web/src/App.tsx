// @integration:begin the export's imports, plus everything it was written without: the API,
// sign-in, and the strings its translation table has no entry for because fixtures never fail.
import { createContext, useContext, useEffect, useState } from "react";
import { signIn, signInAvailable, signOut, useAuth } from "./auth/store";
import { toApiError, type ApiError } from "./api/client";
import {
  createActivity,
  deleteActivity,
  removeAvatar,
  updateActivity,
  updateProfile,
  uploadAvatar,
  uploadCover,
  uploadMedia,
} from "./api/endpoints";
import { toWireActivityType, type ActivityView, type MediaView } from "./api/mappers";
import {
  useActivities,
  useActivity,
  useActivityCount,
  useActivityMedia,
  useProfile,
} from "./data/hooks";
import { failureText, fieldError, loadingText, retryText, signOutText } from "./i18n/failures";
// @integration:end

// ─── Types ────────────────────────────────────────────────────────────────────

type AuthRole = "visitor" | "user" | "admin";
type Theme = "dark" | "light";
type Lang = "en" | "zh";

interface Settings {
  theme: Theme;
  lang: Lang;
  setTheme: (t: Theme) => void;
  setLang: (l: Lang) => void;
}

type ActivityType = "public" | "shared" | "private";

interface Activity {
  id: string;
  title: string;
  location: string;
  activityDate: string;
  description: string;
  coverImageUrl: string | null;
  createdBy: string;
  createdByUserId: string;
  mediaCount: number;
  type: ActivityType;
}

interface MediaItem {
  id: string;
  kind: "Image" | "Video";
  originalFileName: string;
  sizeBytes: number;
  url: string;
  contentType: string;
  uploadedBy: string;
  uploadedByUserId: string;
}

type View =
  | { name: "list" }
  | { name: "detail"; activityId: string }
  | { name: "create" }
  | { name: "edit"; activityId: string }
  | { name: "upload"; activityId: string }
  | { name: "profile" };

// ─── Settings context ────────────────────────────────────────────────────────

const SettingsContext = createContext<Settings>({
  theme: "dark",
  lang: "en",
  setTheme: () => {},
  setLang: () => {},
});

const useSettings = () => useContext(SettingsContext);

// ─── Translations ─────────────────────────────────────────────────────────────

const T = {
  en: {
    newActivity: "New Activity",
    uploadMedia: "Upload media",
    signIn: "Sign in with Microsoft",
    sharedJournal: "Shared Journal",
    heroLine1: "Every trail,",
    heroLine2: "one record.",
    heroDesc: "A communal log of routes taken, summits chased, and coastlines walked. Public by default — media for those who sign in.",
    activitiesLogged: "activities logged",
    visitorCallout: "Sign in to view photos and videos, and to add your own activities to the journal.",
    previous: "Previous",
    next: "Next",
    allActivities: "All activities",
    edit: "Edit",
    delete: "Delete",
    confirm: "Confirm",
    cancel: "Cancel",
    photosVideos: "Photos & Videos",
    signInToView: (n: number) => `Sign in to view ${n} photos and videos`,
    noMedia: "No media uploaded yet.",
    items: (n: number) => `${n} item${n !== 1 ? "s" : ""}`,
    sasRequired: "SAS required",
    details: "Details",
    date: "Date",
    location: "Location",
    loggedBy: "Logged by",
    media: "Media",
    backToJournal: "Back to journal",
    backToActivity: "Back to activity",
    logActivityTitle: "Log an activity",
    editActivityTitle: "Edit activity",
    titleLabel: "Title *",
    titlePlaceholder: "Summit attempt on Glacier Peak",
    titleRequired: "Title is required",
    activityDate: "Activity date *",
    dateRequired: "Date is required",
    locationLabel: "Location *",
    locationPlaceholder: "Mount Rainier National Park, WA",
    locationRequired: "Location is required",
    descriptionLabel: "Description",
    descPlaceholder: "What happened out there…",
    coverImageLabel: "Cover image",
    coverHint: "Public · any size · goes to public container",
    uploadCover: "Upload cover image",
    typeLabel: "Visibility",
    typePublic: "Public",
    typePublicDesc: "Anyone can access",
    typeShared: "Shared",
    typeSharedDesc: "Signed-in users only",
    typePrivate: "Private",
    typePrivateDesc: "Only you",
    logBtn: "Log activity",
    saveBtn: "Save changes",
    activityLogged: "Activity logged.",
    changesSaved: "Changes saved.",
    returningToJournal: "Returning to journal…",
    profile: "Profile",
    profileSubtitle: "Your public identity on the journal.",
    profilePhoto: "Profile photo",
    photoHint: "Shown next to your media uploads. Square image recommended.",
    remove: "Remove",
    displayNameLabel: "Display name *",
    displayNamePlaceholder: "Your name",
    displayNameRequired: "Display name is required",
    bioLabel: "Bio",
    bioPlaceholder: "A few words about you and what you get up to out there…",
    chars: "chars",
    account: "Account — managed by Entra ID",
    email: "Email",
    role: "Role",
    saved: "Saved",
    preferences: "Preferences",
    themeLabel: "Theme",
    themeDark: "Dark",
    themeLight: "Light",
    languageLabel: "Language",
    footerTagline: (n: number) => `One shared journal · ${n} activities`,
  },
  zh: {
    newActivity: "新建活动",
    uploadMedia: "上传媒体",
    signIn: "使用 Microsoft 登录",
    sharedJournal: "共享日志",
    heroLine1: "每一条路，",
    heroLine2: "皆有记录。",
    heroDesc: "一个共同的旅程记录——路线、山峰与海岸。内容公开，媒体文件需登录查看。",
    activitiesLogged: "条活动记录",
    visitorCallout: "登录后可查看照片和视频，并添加您自己的活动记录。",
    previous: "上一页",
    next: "下一页",
    allActivities: "所有活动",
    edit: "编辑",
    delete: "删除",
    confirm: "确认",
    cancel: "取消",
    photosVideos: "照片与视频",
    signInToView: (n: number) => `登录后查看 ${n} 张照片和视频`,
    noMedia: "暂无媒体文件。",
    items: (n: number) => `${n} 个文件`,
    sasRequired: "需要 SAS",
    details: "详情",
    date: "日期",
    location: "地点",
    loggedBy: "记录者",
    media: "媒体",
    backToJournal: "返回日志",
    backToActivity: "返回活动",
    logActivityTitle: "记录活动",
    editActivityTitle: "编辑活动",
    titleLabel: "标题 *",
    titlePlaceholder: "冰川峰登顶尝试",
    titleRequired: "标题为必填项",
    activityDate: "活动日期 *",
    dateRequired: "日期为必填项",
    locationLabel: "地点 *",
    locationPlaceholder: "雷尼尔山国家公园，华盛顿州",
    locationRequired: "地点为必填项",
    descriptionLabel: "描述",
    descPlaceholder: "记录此次经历……",
    coverImageLabel: "封面图片",
    coverHint: "公开 · 任意尺寸 · 上传至公开容器",
    typeLabel: "可见性",
    typePublic: "公开",
    typePublicDesc: "所有人可访问",
    typeShared: "共享",
    typeSharedDesc: "仅登录用户",
    typePrivate: "私密",
    typePrivateDesc: "仅自己",
    uploadCover: "上传封面图片",
    logBtn: "记录活动",
    saveBtn: "保存更改",
    activityLogged: "活动已记录。",
    changesSaved: "更改已保存。",
    returningToJournal: "正在返回日志……",
    profile: "个人资料",
    profileSubtitle: "您在日志中的公开身份。",
    profilePhoto: "头像",
    photoHint: "显示在您的媒体上传旁边。建议使用方形图片。",
    remove: "删除",
    displayNameLabel: "显示名称 *",
    displayNamePlaceholder: "您的姓名",
    displayNameRequired: "显示名称为必填项",
    bioLabel: "个人简介",
    bioPlaceholder: "简单介绍一下您和您的户外经历……",
    chars: "字符",
    account: "账户 — 由 Entra ID 管理",
    email: "邮箱",
    role: "角色",
    saved: "已保存",
    preferences: "偏好设置",
    themeLabel: "主题",
    themeDark: "深色",
    themeLight: "浅色",
    languageLabel: "语言",
    footerTagline: (n: number) => `共享日志 · ${n} 条活动`,
  },
} as const;

// @integration:begin the export's fixtures are replaced by the API
// `MOCK_ACTIVITIES` and `MOCK_MEDIA` are gone rather than emptied: `data/hooks.ts` supplies
// both, and a fixture left here would be a second source of truth that nothing keeps in step
// with the server.
// @integration:end

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(dateStr: string, lang: Lang) {
  const d = new Date(dateStr + "T12:00:00");
  return d.toLocaleDateString(lang === "zh" ? "zh-CN" : "en-US", {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function formatBytes(bytes: number) {
  if (bytes >= 1e6) return (bytes / 1e6).toFixed(1) + " MB";
  return (bytes / 1e3).toFixed(0) + " KB";
}

// Shared style helpers based on CSS variables
const C = {
  bg: "bg-[var(--tb-bg)]",
  fg: "text-[var(--tb-fg)]",
  card: "bg-[var(--tb-card)]",
  cardHover: "hover:bg-[var(--tb-card)]",
  secondary: "bg-[var(--tb-secondary)]",
  muted: "bg-[var(--tb-muted)]",
  dim: "text-[var(--tb-dim)]",
  secondaryFg: "text-[var(--tb-secondary-fg)]",
  border: "border-[var(--tb-border)]",
  borderBg: "bg-[var(--tb-border)]",
  dimmer: "text-[var(--tb-dimmer)]",
};

// ─── Activity type badge ───────────────────────────────────────────────────────

function ActivityTypeBadge({ type }: { type: ActivityType }) {
  const { lang } = useSettings();
  const t = T[lang];
  const cfg: Record<ActivityType, { label: string; icon: React.ReactNode; color: string }> = {
    public: {
      label: t.typePublic,
      color: "text-emerald-500",
      icon: (
        <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
          <circle cx="5" cy="5" r="4" stroke="currentColor" strokeWidth="1.2" />
          <path d="M1.5 5C1.5 5 3 7.5 5 7.5C7 7.5 8.5 5 8.5 5C8.5 5 7 2.5 5 2.5C3 2.5 1.5 5 1.5 5Z" stroke="currentColor" strokeWidth="1.2" />
          <circle cx="5" cy="5" r="1.2" fill="currentColor" />
        </svg>
      ),
    },
    shared: {
      label: t.typeShared,
      color: "text-sky-400",
      icon: (
        <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
          <circle cx="3.5" cy="4" r="1.5" stroke="currentColor" strokeWidth="1.2" />
          <circle cx="6.5" cy="4" r="1.5" stroke="currentColor" strokeWidth="1.2" />
          <path d="M1 8.5C1 7.12 2.12 6 3.5 6H6.5C7.88 6 9 7.12 9 8.5" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
        </svg>
      ),
    },
    private: {
      label: t.typePrivate,
      color: "text-[#c8893a]",
      icon: (
        <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
          <rect x="2" y="4.5" width="6" height="5" rx="0.8" stroke="currentColor" strokeWidth="1.2" />
          <path d="M3.5 4.5V3C3.5 2.17 4.17 1.5 5 1.5C5.83 1.5 6.5 2.17 6.5 3V4.5" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
        </svg>
      ),
    },
  };
  const { label, icon, color } = cfg[type];
  return (
    <span className={`inline-flex items-center gap-1 font-mono-data text-[10px] uppercase tracking-wider ${color}`}>
      {icon}
      {label}
    </span>
  );
}

// ─── Nav ──────────────────────────────────────────────────────────────────────

function Nav({
  authRole,
  currentView,
  onNavigate,
}: {
  authRole: AuthRole;
  currentView: View;
  onNavigate: (v: View) => void;
}) {
  const { lang } = useSettings();
  const t = T[lang];
  const isDetail = currentView?.name === "detail";
  const plusLabel = isDetail ? t.uploadMedia : t.newActivity;
  const detailActivityId = isDetail ? (currentView as { name: "detail"; activityId: string }).activityId : null;
  const plusTarget: View = detailActivityId
    ? { name: "upload", activityId: detailActivityId }
    : { name: "create" };

  return (
    <header className={`fixed top-0 left-0 right-0 z-50 border-b ${C.border} ${C.bg}/95 backdrop-blur-sm transition-colors duration-300`}>
      <div className="max-w-6xl mx-auto px-6 h-14 flex items-center justify-between">
        <button onClick={() => onNavigate({ name: "list" })} className="flex items-center gap-3 group">
          <span className="text-[#c8893a] text-lg leading-none">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
              <path d="M10 2L13 7H17L14 11L15.5 16L10 13L4.5 16L6 11L3 7H7L10 2Z" fill="currentColor" opacity="0.9" />
            </svg>
          </span>
          <span className={`font-display font-semibold text-[15px] tracking-wide ${C.fg} group-hover:text-[#c8893a] transition-colors`}>
            TrailBlaze
          </span>
        </button>

        <div className="flex items-center gap-4">
          {authRole !== "visitor" && (
            <button
              onClick={() => onNavigate(plusTarget)}
              className={`hidden sm:flex items-center gap-2 text-sm font-medium ${C.dim} hover:text-[var(--tb-fg)] transition-colors`}
            >
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path d="M7 1V13M1 7H13" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              </svg>
              {plusLabel}
            </button>
          )}

          {authRole === "visitor" ? (
            // @integration:begin sign-in is an MSAL popup, not a link to a route this app does not have
            <button
              type="button"
              onClick={() => void signIn()}
              disabled={!signInAvailable}
              title={signInAvailable ? undefined : "This deployment has no Entra tenant configured."}
              className={`flex items-center gap-2 text-sm font-medium ${C.dim} hover:text-[var(--tb-fg)] disabled:opacity-40 disabled:cursor-not-allowed transition-colors`}
            >
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path d="M9 2H12C12.5523 2 13 2.44772 13 3V11C13 11.5523 12.5523 12 12 12H9" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
                <path d="M6 9.5L9 7L6 4.5" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M1 7H9" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
              </svg>
              {t.signIn}
            </button>
            // @integration:end
          ) : (
            <button onClick={() => onNavigate({ name: "profile" })} className="flex items-center gap-2 hover:opacity-80 transition-opacity">
              <span className="w-6 h-6 rounded-full bg-[#c8893a] flex items-center justify-center text-[10px] font-semibold text-[#0f120e]">
                {authRole === "admin" ? "A" : "U"}
              </span>
              <span className={`hidden sm:block text-sm ${C.secondaryFg} capitalize`}>{authRole}</span>
            </button>
          )}
        </div>
      </div>
    </header>
  );
}

// ─── Activity Card ─────────────────────────────────────────────────────────────

function ActivityCard({ activity, onClick }: { activity: Activity; onClick: () => void }) {
  const { lang } = useSettings();

  return (
    <article
      className={`group cursor-pointer border ${C.border} hover:border-[var(--tb-dim)] transition-all duration-300 ${C.card}`}
      onClick={onClick}
    >
      <div className={`aspect-[16/9] overflow-hidden ${C.muted} relative`}>
        {activity.coverImageUrl ? (
          <img src={activity.coverImageUrl} alt={activity.title} className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            <svg width="32" height="32" viewBox="0 0 32 32" fill="none" className="opacity-20">
              <path d="M4 24L11 14L16 20L21 16L28 24H4Z" stroke="currentColor" strokeWidth="1.5" fill="none" />
            </svg>
          </div>
        )}
        <div className={`absolute inset-0 bg-gradient-to-t from-[var(--tb-card)]/60 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300`} />
      </div>

      <div className="p-5">
        <div className="flex items-center gap-3 mb-3">
          <span className="font-mono-data text-[11px] text-[#c8893a] tracking-wider">
            {formatDate(activity.activityDate, lang)}
          </span>
          <span className={`w-1 h-1 rounded-full ${C.borderBg}`} />
          <span className={`text-[11px] ${C.dim} truncate`}>{activity.location}</span>
        </div>
        <h2 className={`font-display font-semibold text-[18px] leading-snug ${C.fg} group-hover:text-white transition-colors mb-2 line-clamp-2`}>
          {activity.title}
        </h2>
        <p className={`text-sm ${C.dim} line-clamp-2 leading-relaxed mb-4`}>{activity.description}</p>
        <div className={`flex items-center justify-between pt-3 border-t ${C.border}`}>
          <span className={`text-[12px] ${C.dim}`}>{activity.createdBy}</span>
          <div className="flex items-center gap-3">
            <ActivityTypeBadge type={activity.type} />
            <span className={`font-mono-data text-[11px] ${C.dimmer} tracking-wider`}>{activity.mediaCount} media</span>
          </div>
        </div>
      </div>
    </article>
  );
}

// ─── Activity List ─────────────────────────────────────────────────────────────

function ActivityList({ authRole, onNavigate }: { authRole: AuthRole; onNavigate: (v: View) => void }) {
  const { lang } = useSettings();
  const t = T[lang];
  const [page, setPage] = useState(1);
  // @integration:begin one page at a time from the API, and the count of everything readable
  // `pageSize` is the server's, not the 6 asked for: it clamps, and the pager steps by what it
  // actually applied rather than by what this screen requested.
  const { items: paged, total, pageSize, loading, error, reload } = useActivities(page - 1, 6);
  const totalPages = Math.ceil(total / pageSize);
  // @integration:end

  return (
    <main className="pt-14">
      <div className="max-w-6xl mx-auto px-6">
        <div className={`py-16 border-b ${C.border} mb-12`}>
          <div className="grid grid-cols-1 lg:grid-cols-[1fr_auto] gap-8 items-end">
            <div>
              <p className="font-mono-data text-[11px] text-[#c8893a] tracking-widest uppercase mb-4">{t.sharedJournal}</p>
              <h1 className={`font-display text-[52px] lg:text-[68px] leading-[0.95] font-light ${C.fg}`}>
                {t.heroLine1}
                <br />
                <em className="font-light not-italic text-[#c8893a]">{t.heroLine2}</em>
              </h1>
              <p className={`mt-5 text-[15px] ${C.dim} max-w-md leading-relaxed`}>{t.heroDesc}</p>
            </div>
            <div className="lg:text-right">
              <div className="inline-flex flex-col gap-1">
                <span className={`font-mono-data text-[36px] font-medium ${C.fg}`}>{total}</span>
                <span className={`text-[12px] ${C.dim} uppercase tracking-widest`}>{t.activitiesLogged}</span>
              </div>
            </div>
          </div>
        </div>

        {authRole === "visitor" && (
          <div className={`mb-10 flex items-start gap-4 px-5 py-4 border ${C.border} ${C.card}`}>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" className="mt-0.5 shrink-0 text-[#c8893a]">
              <circle cx="8" cy="8" r="7" stroke="currentColor" strokeWidth="1.2" />
              <path d="M8 7V11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              <circle cx="8" cy="5" r="0.75" fill="currentColor" />
            </svg>
            <p className={`text-sm ${C.secondaryFg}`}>{t.visitorCallout}</p>
          </div>
        )}

        {/* @integration:begin the list's own loading and refusal states, which the fixtures had no way to reach */}
        {loading && paged.length === 0 && (
          <p className={`text-sm ${C.dim} py-10`}>{loadingText(lang)}</p>
        )}
        {error && (
          <div className={`mb-10 flex items-start gap-4 px-5 py-4 border ${C.border} ${C.card}`}>
            <div>
              <p className={`text-sm ${C.fg} font-medium`}>{failureText(lang, error).title}</p>
              <p className={`text-[13px] ${C.dim} mt-1 leading-relaxed`}>{failureText(lang, error).detail}</p>
              <button onClick={reload} className="mt-3 text-[13px] text-[#c8893a] hover:underline">
                {retryText(lang)}
              </button>
            </div>
          </div>
        )}
        {/* @integration:end */}

        <div className={`grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-px ${C.borderBg}`}>
          {paged.map((a) => (
            <div key={a.id} className={C.bg}>
              <ActivityCard activity={a} onClick={() => onNavigate({ name: "detail", activityId: a.id })} />
            </div>
          ))}
        </div>

        {totalPages > 1 && (
          <div className={`flex items-center justify-between py-10 border-t ${C.border} mt-px`}>
            <button
              disabled={page === 1}
              onClick={() => setPage(page - 1)}
              className={`flex items-center gap-2 text-sm ${C.dim} hover:text-[var(--tb-fg)] disabled:opacity-30 disabled:cursor-not-allowed transition-colors`}
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path d="M10 12L6 8L10 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              {t.previous}
            </button>
            <span className={`font-mono-data text-[12px] ${C.dim}`}>{page} / {totalPages}</span>
            <button
              disabled={page === totalPages}
              onClick={() => setPage(page + 1)}
              className={`flex items-center gap-2 text-sm ${C.dim} hover:text-[var(--tb-fg)] disabled:opacity-30 disabled:cursor-not-allowed transition-colors`}
            >
              {t.next}
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path d="M6 4L10 8L6 12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>
          </div>
        )}
      </div>
    </main>
  );
}

// ─── Activity Detail ───────────────────────────────────────────────────────────

function ActivityDetail({
  activityId, authRole, currentUserId, onNavigate, onDelete,
}: {
  activityId: string;
  authRole: AuthRole;
  currentUserId: string;
  onNavigate: (v: View) => void;
  onDelete: (id: string) => void;
}) {
  const { lang, theme } = useSettings();
  const t = T[lang];

  // @integration:begin the activity and its media come from the API
  const { activity: loaded, loading, error } = useActivity(activityId);
  const activity = loaded ?? undefined;
  const canViewMedia = authRole !== "visitor";
  // Not fetched at all for a visitor: the route is signed-in only, and a signed URL is what it
  // hands back, so an anonymous caller asking for one is a 401 rather than a list.
  const { media, error: mediaError, reload: reloadMedia } = useActivityMedia(activityId, canViewMedia);
  const isOwner = activity?.createdByUserId === currentUserId;
  const canEdit = authRole === "admin" || (authRole === "user" && isOwner);
  // @integration:end

  const mediaByUploader = media.reduce<{ userId: string; name: string; items: MediaItem[] }[]>(
    (groups, item) => {
      const existing = groups.find((g) => g.userId === item.uploadedByUserId);
      if (existing) { existing.items.push(item); }
      else { groups.push({ userId: item.uploadedByUserId, name: item.uploadedBy, items: [item] }); }
      return groups;
    }, []
  );

  const allImages = media.filter((m) => m.kind === "Image");

  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);
  const [collapsedUploaders, setCollapsedUploaders] = useState<Set<string>>(
    () => new Set(mediaByUploader.slice(1).map((g) => g.userId))
  );
  const [confirmDelete, setConfirmDelete] = useState(false);

  function toggleUploader(userId: string) {
    setCollapsedUploaders((prev) => {
      const next = new Set(prev);
      next.has(userId) ? next.delete(userId) : next.add(userId);
      return next;
    });
  }

  // @integration:begin the delete goes through the API, and a refusal keeps the page as it was
  const [deleteError, setDeleteError] = useState<ApiError | null>(null);

  async function handleDelete() {
    if (!activity) return;
    try {
      await deleteActivity(activity.id);
    } catch (failure) {
      // A 403 or a 404 here is not a success: staying put is what keeps the screen from
      // showing a deletion that did not happen.
      setDeleteError(toApiError(failure));
      return;
    }
    onDelete(activity.id);
  }
  // @integration:end

  // @integration:begin loading, refused and absent are three different screens here
  if (loading) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <p className={C.dim}>{loadingText(lang)}</p>
      </main>
    );
  }

  if (error) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <div className="text-center max-w-md px-6">
          <p className={`font-display text-[22px] ${C.fg}`}>{failureText(lang, error).title}</p>
          <p className={`text-sm ${C.dim} mt-2 leading-relaxed`}>{failureText(lang, error).detail}</p>
        </div>
      </main>
    );
  }
  // @integration:end

  if (!activity) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <p className={C.dim}>Activity not found.</p>
      </main>
    );
  }

  return (
    <main className="pt-14">
      <div className={`relative h-[55vh] min-h-[320px] ${C.muted}`}>
        {activity.coverImageUrl && (
          <img src={activity.coverImageUrl} alt={activity.title} className="absolute inset-0 w-full h-full object-cover" />
        )}
        <div className={`absolute inset-0 bg-gradient-to-t from-[var(--tb-bg)] via-[var(--tb-bg)]/40 to-transparent`} />

        <div className="absolute bottom-0 left-0 right-0 max-w-6xl mx-auto px-6 pb-10">
          <button onClick={() => onNavigate({ name: "list" })} className={`flex items-center gap-2 text-sm ${C.secondaryFg} hover:text-[var(--tb-fg)] transition-colors mb-6`}>
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
              <path d="M9 11L5 7L9 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            {t.allActivities}
          </button>

          <div className="flex items-end justify-between gap-4 flex-wrap">
            <div>
              <div className="flex items-center gap-3 mb-3">
                <span className="font-mono-data text-[11px] text-[#c8893a] tracking-wider">{formatDate(activity.activityDate, lang)}</span>
                <span className={C.dimmer}>·</span>
                <span className={`text-[12px] ${C.secondaryFg}`}>{activity.location}</span>
              </div>
              <h1 className="font-display font-semibold text-[36px] lg:text-[48px] leading-tight text-white max-w-3xl">
                {activity.title}
              </h1>
            </div>

            {canEdit && (
              <div className="flex items-center gap-2 shrink-0">
                <button
                  onClick={() => onNavigate({ name: "edit", activityId: activity.id })}
                  className={`px-4 py-2 text-sm font-medium ${C.fg} border ${C.border} hover:border-[#c8893a] hover:text-[#c8893a] transition-colors`}
                >
                  {t.edit}
                </button>
                {!confirmDelete ? (
                  <button onClick={() => setConfirmDelete(true)} className={`px-4 py-2 text-sm font-medium ${C.dim} border ${C.border} hover:border-red-800 hover:text-red-400 transition-colors`}>
                    {t.delete}
                  </button>
                ) : (
                  <div className="flex items-center gap-1">
                    {/* @integration:begin the refusal is shown where the action was taken */}
                    <button onClick={() => void handleDelete()} className="px-3 py-2 text-sm font-medium text-red-400 border border-red-800 hover:bg-red-950 transition-colors">
                      {t.confirm}
                    </button>
                    {/* @integration:end */}
                    <button onClick={() => setConfirmDelete(false)} className={`px-3 py-2 text-sm ${C.dim} hover:text-[var(--tb-fg)] transition-colors`}>
                      {t.cancel}
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* @integration:begin the refusals a fixture could not produce: a failed delete, and media the reader may not mint URLs for */}
      {(deleteError || mediaError) && (
        <div className="max-w-6xl mx-auto px-6 pt-8">
          {[deleteError, mediaError].filter((e): e is ApiError => e !== null).map((e, i) => (
            <div key={i} className={`flex items-start justify-between gap-4 px-5 py-4 mb-2 border ${C.border} ${C.card}`}>
              <div>
                <p className={`text-sm ${C.fg} font-medium`}>{failureText(lang, e).title}</p>
                <p className={`text-[13px] ${C.dim} mt-1 leading-relaxed`}>{failureText(lang, e).detail}</p>
              </div>
              {e === mediaError && (
                <button onClick={reloadMedia} className="text-[13px] text-[#c8893a] hover:underline shrink-0">
                  {retryText(lang)}
                </button>
              )}
            </div>
          ))}
        </div>
      )}
      {/* @integration:end */}

      <div className="max-w-6xl mx-auto px-6 py-12">
        <div className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-12">
          <div>
            {activity.description && (
              <p className={`text-[16px] ${C.secondaryFg} leading-[1.8] mb-10`}>{activity.description}</p>
            )}

            <section>
              <h2 className={`font-display font-semibold text-[22px] ${C.fg} mb-6`}>{t.photosVideos}</h2>

              {authRole === "visitor" ? (
                <div className={`border border-dashed ${C.border} px-8 py-12 text-center`}>
                  <div className={`w-10 h-10 mx-auto mb-4 flex items-center justify-center border ${C.border}`}>
                    <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
                      <rect x="3" y="7" width="12" height="9" rx="1" stroke="currentColor" strokeWidth="1.2" className={C.dim} />
                      <path d="M6 7V5C6 3.34315 7.34315 2 9 2C10.6569 2 12 3.34315 12 5V7" stroke="currentColor" strokeWidth="1.2" className={C.dim} />
                    </svg>
                  </div>
                  <p className={`text-[14px] ${C.dim}`}>{t.signInToView(activity.mediaCount)}</p>
                </div>
              ) : mediaByUploader.length === 0 ? (
                <div className={`border border-dashed ${C.border} px-8 py-12 text-center`}>
                  <p className={`text-[14px] ${C.dim}`}>{t.noMedia}</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {mediaByUploader.map((group) => {
                    const groupImages = group.items.filter((m) => m.kind === "Image");
                    const groupVideos = group.items.filter((m) => m.kind === "Video");
                    const isCollapsed = collapsedUploaders.has(group.userId);
                    return (
                      <div key={group.userId} className={`border ${C.border}`}>
                        <button
                          onClick={() => toggleUploader(group.userId)}
                          className={`w-full flex items-center gap-3 px-4 py-3 ${C.cardHover} transition-colors`}
                        >
                          <div className={`w-6 h-6 rounded-full ${C.secondary} border ${C.border} flex items-center justify-center text-[10px] font-semibold text-[#c8893a] shrink-0`}>
                            {group.name.charAt(0)}
                          </div>
                          <span className={`text-[13px] ${C.secondaryFg} flex-1 text-left`}>{group.name}</span>
                          <span className={`font-mono-data text-[11px] ${C.dimmer} mr-2`}>{t.items(group.items.length)}</span>
                          <svg width="12" height="12" viewBox="0 0 12 12" fill="none" className={`${C.dim} transition-transform duration-200 ${isCollapsed ? "-rotate-90" : ""}`}>
                            <path d="M2 4L6 8L10 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                          </svg>
                        </button>

                        {!isCollapsed && (
                          <div className={`px-4 pb-4 pt-1 border-t ${C.border}`}>
                            {groupImages.length > 0 && (
                              <div className="grid grid-cols-2 md:grid-cols-3 gap-1 mb-3 mt-3">
                                {groupImages.map((m) => {
                                  const globalIdx = allImages.findIndex((img) => img.id === m.id);
                                  return (
                                    <button key={m.id} className={`aspect-square overflow-hidden ${C.muted} group relative`} onClick={() => setLightboxIndex(globalIdx)}>
                                      <img src={m.url} alt={m.originalFileName} className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105" />
                                      <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors" />
                                    </button>
                                  );
                                })}
                              </div>
                            )}
                            {groupVideos.map((m) => (
                              <div key={m.id} className={`flex items-center gap-4 px-4 py-3 border ${C.border} mb-2 mt-2`}>
                                <div className={`w-8 h-8 ${C.secondary} flex items-center justify-center shrink-0`}>
                                  <svg width="12" height="14" viewBox="0 0 12 14" fill="none">
                                    <path d="M1 1L11 7L1 13V1Z" fill="#c8893a" />
                                  </svg>
                                </div>
                                <div className="flex-1 min-w-0">
                                  <p className={`text-sm ${C.fg} truncate`}>{m.originalFileName}</p>
                                  <p className={`font-mono-data text-[11px] ${C.dim}`}>{formatBytes(m.sizeBytes)} · {m.contentType}</p>
                                </div>
                                <span className={`font-mono-data text-[10px] ${C.dimmer} uppercase tracking-wider`}>{t.sasRequired}</span>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </section>
          </div>

          <aside>
            <div className={`border ${C.border} p-5`}>
              <h3 className={`font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-4`}>{t.details}</h3>
              <dl className="space-y-4">
                <div>
                  <dt className={`text-[11px] ${C.dim} mb-1`}>{t.date}</dt>
                  <dd className={`text-[14px] ${C.fg}`}>{formatDate(activity.activityDate, lang)}</dd>
                </div>
                <div>
                  <dt className={`text-[11px] ${C.dim} mb-1`}>{t.location}</dt>
                  <dd className={`text-[14px] ${C.fg}`}>{activity.location}</dd>
                </div>
                <div>
                  <dt className={`text-[11px] ${C.dim} mb-1`}>{t.loggedBy}</dt>
                  <dd className={`text-[14px] ${C.fg}`}>{activity.createdBy}</dd>
                </div>
                <div>
                  <dt className={`text-[11px] ${C.dim} mb-1`}>{t.typeLabel}</dt>
                  <dd className="mt-1"><ActivityTypeBadge type={activity.type} /></dd>
                </div>
                <div>
                  <dt className={`text-[11px] ${C.dim} mb-1`}>{t.media}</dt>
                  <dd className={`font-mono-data text-[14px] ${C.fg}`}>{activity.mediaCount} items</dd>
                </div>
              </dl>
            </div>
          </aside>
        </div>
      </div>

      {lightboxIndex !== null && (
        <div className="fixed inset-0 z-50 bg-black/95 flex items-center justify-center" onClick={() => setLightboxIndex(null)}>
          <button className="absolute top-4 right-4 text-[#7a7568] hover:text-white transition-colors" onClick={() => setLightboxIndex(null)}>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path d="M18 6L6 18M6 6L18 18" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
            </svg>
          </button>
          <button className="absolute left-4 top-1/2 -translate-y-1/2 text-[#7a7568] hover:text-white transition-colors p-2" onClick={(e) => { e.stopPropagation(); setLightboxIndex((prev) => prev !== null ? (prev - 1 + allImages.length) % allImages.length : 0); }}>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path d="M15 18L9 12L15 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
          <img src={allImages[lightboxIndex]?.url} alt={allImages[lightboxIndex]?.originalFileName} className="max-w-4xl max-h-[85vh] object-contain" onClick={(e) => e.stopPropagation()} />
          <button className="absolute right-4 top-1/2 -translate-y-1/2 text-[#7a7568] hover:text-white transition-colors p-2" onClick={(e) => { e.stopPropagation(); setLightboxIndex((prev) => prev !== null ? (prev + 1) % allImages.length : 0); }}>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path d="M9 18L15 12L9 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
          <div className="absolute bottom-4 left-1/2 -translate-x-1/2 font-mono-data text-[11px] text-[#7a7568]">
            {lightboxIndex + 1} / {allImages.length}
          </div>
        </div>
      )}
    </main>
  );
}

// ─── Activity Form ─────────────────────────────────────────────────────────────

function ActivityForm({ mode, activityId, onNavigate }: { mode: "create" | "edit"; activityId?: string; onNavigate: (v: View) => void }) {
  const { lang, theme } = useSettings();
  const t = T[lang];
  // @integration:begin the form is filled from the API and saved to it
  const { activity: existing } = useActivity(activityId ?? "");
  const [cover, setCover] = useState<File | null>(null);
  const [filledFor, setFilledFor] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<ApiError | null>(null);

  const [form, setForm] = useState({ title: "", location: "", activityDate: "", description: "", type: "public" as ActivityType });
  const [coverPreview, setCoverPreview] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Fills the form when the edit target arrives, once. Re-running would overwrite whatever the
  // person has typed since the request finished.
  if (existing && filledFor !== existing.id) {
    setFilledFor(existing.id);
    setForm({ title: existing.title, location: existing.location, activityDate: existing.activityDate, description: existing.description, type: existing.type });
    setCoverPreview(existing.coverImageUrl);
  }
  // @integration:end

  function validate() {
    const e: Record<string, string> = {};
    if (!form.title.trim()) e.title = t.titleRequired;
    if (!form.location.trim()) e.location = t.locationRequired;
    if (!form.activityDate) e.activityDate = t.dateRequired;
    return e;
  }

  // @integration:begin the picker the export's cover button never had
  function handleCoverChange(evt: React.ChangeEvent<HTMLInputElement>) {
    const file = evt.target.files?.[0];
    if (!file) return;
    setCover(file);
    setCoverPreview(URL.createObjectURL(file));
    // Cleared so picking the same file twice fires `change` the second time.
    evt.target.value = "";
  }
  // @integration:end

  // @integration:begin create, edit and the cover, over the API
  async function handleSubmit(evt: React.FormEvent) {
    evt.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }

    setSubmitting(true);
    setSubmitError(null);
    try {
      const body = {
        title: form.title,
        location: form.location,
        activityDate: form.activityDate,
        description: form.description,
        type: toWireActivityType(form.type),
      };
      const saved = existing
        ? await updateActivity(existing.id, body)
        : await createActivity(body);

      // A second request, because a cover attaches to an activity that already exists.
      if (cover) await uploadCover(saved.id, cover);

      setSubmitted(true);
      setTimeout(() => onNavigate({ name: "list" }), 1400);
    } catch (failure) {
      const apiError = toApiError(failure);
      // The server's field-keyed rejections land under the fields this form renders.
      const fieldErrors: Record<string, string> = {};
      for (const field of ["title", "location", "activityDate"]) {
        const message = fieldError(apiError, field);
        if (message) fieldErrors[field] = message;
      }
      const named = Object.keys(apiError.problem?.errors ?? {});
      const allShown = named.length > 0 && named.every((field) => field in fieldErrors);
      setErrors(fieldErrors);
      // A refusal, or a rule about a field this form has no slot for, goes in the banner
      // rather than being dropped on the floor.
      setSubmitError(allShown ? null : apiError);
    } finally {
      setSubmitting(false);
    }
  }
  // @integration:end

  const inputClass = (hasError: boolean) =>
    `w-full ${C.card} border px-4 py-3 text-[15px] ${C.fg} placeholder-[var(--tb-dimmer)] focus:outline-none focus:border-[#c8893a] transition-colors ${hasError ? "border-red-700" : C.border}`;

  if (submitted) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 border border-[#c8893a] flex items-center justify-center mx-auto mb-4">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
              <path d="M4 10L8 14L16 6" stroke="#c8893a" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <p className={`font-display text-[22px] ${C.fg}`}>{mode === "create" ? t.activityLogged : t.changesSaved}</p>
          <p className={`text-sm ${C.dim} mt-1`}>{t.returningToJournal}</p>
        </div>
      </main>
    );
  }

  return (
    <main className="pt-14">
      <div className="max-w-3xl mx-auto px-6 py-12">
        <button onClick={() => onNavigate(activityId ? { name: "detail", activityId } : { name: "list" })} className={`flex items-center gap-2 text-sm ${C.dim} hover:text-[var(--tb-fg)] transition-colors mb-8`}>
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
            <path d="M9 11L5 7L9 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          {mode === "create" ? t.backToJournal : t.backToActivity}
        </button>

        <h1 className={`font-display text-[38px] font-semibold ${C.fg} mb-8`}>
          {mode === "create" ? t.logActivityTitle : t.editActivityTitle}
        </h1>

        <form onSubmit={handleSubmit} className="space-y-6">
          <div>
            <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.titleLabel}</label>
            <input type="text" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder={t.titlePlaceholder} className={inputClass(!!errors.title)} />
            {errors.title && <p className="mt-1 text-[12px] text-red-400">{errors.title}</p>}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.activityDate}</label>
              <input
                type="date"
                value={form.activityDate}
                onChange={(e) => setForm({ ...form, activityDate: e.target.value })}
                className={`${inputClass(!!errors.activityDate)} ${theme === "dark" ? "[color-scheme:dark]" : "[color-scheme:light]"}`}
              />
              {errors.activityDate && <p className="mt-1 text-[12px] text-red-400">{errors.activityDate}</p>}
            </div>
            <div>
              <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.locationLabel}</label>
              <input type="text" value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} placeholder={t.locationPlaceholder} className={inputClass(!!errors.location)} />
              {errors.location && <p className="mt-1 text-[12px] text-red-400">{errors.location}</p>}
            </div>
          </div>

          <div>
            <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.descriptionLabel}</label>
            <textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder={t.descPlaceholder} rows={5} className={`${inputClass(false)} resize-none`} />
          </div>

          {/* Visibility */}
          <div>
            <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-3`}>{t.typeLabel}</label>
            <div className="grid grid-cols-3 gap-2">
              {(["public", "shared", "private"] as ActivityType[]).map((opt) => {
                const labelKey = `type${opt.charAt(0).toUpperCase() + opt.slice(1)}` as "typePublic" | "typeShared" | "typePrivate";
                const descKey = `${labelKey}Desc` as "typePublicDesc" | "typeSharedDesc" | "typePrivateDesc";
                const selected = form.type === opt;
                return (
                  <button
                    key={opt}
                    type="button"
                    onClick={() => setForm({ ...form, type: opt })}
                    className={`flex flex-col items-start gap-1 px-4 py-3 border transition-colors text-left ${
                      selected
                        ? "border-[#c8893a] bg-[#c8893a]/5"
                        : `${C.border} ${C.card} hover:border-[var(--tb-dim)]`
                    }`}
                  >
                    <span className={selected ? "text-[#c8893a]" : ""}>
                      <ActivityTypeBadge type={opt} />
                    </span>
                    <span className={`text-[11px] ${C.dim} leading-snug`}>{t[descKey]}</span>
                  </button>
                );
              })}
            </div>
          </div>

          <div>
            <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.coverImageLabel}</label>
            <div className={`border border-dashed ${C.border} hover:border-[#c8893a]/40 transition-colors`}>
              {/* @integration:begin the export's cover picker, given the two things it was missing:
                  the discard button also drops a pending file, and the "Upload cover image" button
                  — which rendered inert — is a label that can carry one. Its markup is untouched. */}
              {coverPreview ? (
                <div className="relative">
                  <img src={coverPreview} alt="Cover preview" className="w-full h-40 object-cover" />
                  <button type="button" onClick={() => { setCover(null); setCoverPreview(null); }} title={mode === "edit" && !cover ? "Removing a stored cover is not an API operation; this only discards an unsaved pick." : undefined} className={`absolute top-2 right-2 w-7 h-7 ${C.bg}/80 flex items-center justify-center ${C.dim} hover:text-[var(--tb-fg)] transition-colors`}>
                    <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                      <path d="M9 3L3 9M3 3L9 9" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                    </svg>
                  </button>
                </div>
              ) : (
                <label className={`w-full px-6 py-8 flex flex-col items-center gap-2 ${C.dim} hover:text-[var(--tb-fg)] transition-colors cursor-pointer`}>
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
                    <path d="M21 15V19C21 20.1046 20.1046 21 19 21H5C3.89543 21 3 20.1046 3 19V15" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
                    <path d="M17 8L12 3L7 8" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" />
                    <path d="M12 3V15" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
                  </svg>
                  <span className="text-sm">{t.uploadCover}</span>
                  <span className={`font-mono-data text-[11px] ${C.dimmer}`}>{t.coverHint}</span>
                  <input type="file" accept="image/jpeg,image/png,image/webp,image/gif" className="hidden" onChange={handleCoverChange} />
                </label>
              )}
              {/* @integration:end */}
            </div>
          </div>

          {/* @integration:begin the refusal the form's own validation cannot produce */}
          {submitError && (
            <div className={`px-5 py-4 border ${C.border} ${C.card}`}>
              <p className={`text-sm ${C.fg} font-medium`}>{failureText(lang, submitError).title}</p>
              <p className={`text-[13px] ${C.dim} mt-1 leading-relaxed`}>{failureText(lang, submitError).detail}</p>
            </div>
          )}
          {/* @integration:end */}

          <div className={`flex items-center gap-3 pt-4 border-t ${C.border}`}>
            {/* @integration:begin a submit already in flight must not be sent twice */}
            <button type="submit" disabled={submitting} className="px-6 py-3 bg-[#c8893a] text-[#0f120e] text-sm font-semibold hover:bg-[#d9a050] disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
              {mode === "create" ? t.logBtn : t.saveBtn}
            </button>
            {/* @integration:end */}
            <button type="button" onClick={() => onNavigate(activityId ? { name: "detail", activityId } : { name: "list" })} className={`px-6 py-3 text-sm ${C.dim} hover:text-[var(--tb-fg)] transition-colors`}>
              {t.cancel}
            </button>
          </div>
        </form>
      </div>
    </main>
  );
}

// ─── Media Upload Form ─────────────────────────────────────────────────────────

function MediaUploadForm({ activityId, onNavigate }: { activityId: string; onNavigate: (v: View) => void }) {
  const { lang } = useSettings();
  const t = T[lang];
  // @integration:begin the activity's title, which the header shows
  const { activity } = useActivity(activityId);
  // @integration:end

  type FileEntry = { id: string; file: File; preview: string | null; kind: "Image" | "Video" };
  const [files, setFiles] = useState<FileEntry[]>([]);
  const [submitted, setSubmitted] = useState(false);
  const [dragOver, setDragOver] = useState(false);

  function addFiles(incoming: FileList | null) {
    if (!incoming) return;
    const entries: FileEntry[] = Array.from(incoming).map((file) => {
      const kind: "Image" | "Video" = file.type.startsWith("video/") ? "Video" : "Image";
      const preview = kind === "Image" ? URL.createObjectURL(file) : null;
      return { id: Math.random().toString(36).slice(2), file, preview, kind };
    });
    setFiles((prev) => [...prev, ...entries]);
  }

  function removeFile(id: string) {
    setFiles((prev) => prev.filter((f) => f.id !== id));
  }

  // @integration:begin one request per file, so one refusal does not discard the rest
  const [progress, setProgress] = useState({ done: 0, total: 0 });
  const [refusals, setRefusals] = useState<{ name: string; error: ApiError }[]>([]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!files.length) return;

    setRefusals([]);
    setProgress({ done: 0, total: files.length });

    const failed: { name: string; error: ApiError }[] = [];
    for (const entry of files) {
      try {
        await uploadMedia(activityId, entry.file);
      } catch (failure) {
        // An oversize or disallowed file is refused by the server with the reason, and the
        // reason is what the person needs to see — the rest of the batch still goes.
        failed.push({ name: entry.file.name, error: toApiError(failure) });
      }
      setProgress((prev) => ({ ...prev, done: prev.done + 1 }));
    }

    if (failed.length) {
      setRefusals(failed);
      // Only the refused files stay, so a second submit retries exactly them.
      const refusedIds = new Set(files.filter((f) => failed.some((x) => x.name === f.file.name)).map((f) => f.id));
      setFiles((prev) => prev.filter((f) => refusedIds.has(f.id)));
      return;
    }

    setSubmitted(true);
    setTimeout(() => onNavigate({ name: "detail", activityId }), 1400);
  }
  // @integration:end

  if (submitted) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 border border-[#c8893a] flex items-center justify-center mx-auto mb-4">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
              <path d="M4 10L8 14L16 6" stroke="#c8893a" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <p className={`font-display text-[22px] ${C.fg}`}>{t.changesSaved}</p>
          <p className={`text-sm ${C.dim} mt-1`}>{t.returningToJournal}</p>
        </div>
      </main>
    );
  }

  return (
    <main className="pt-14">
      <div className="max-w-3xl mx-auto px-6 py-12">
        <button
          onClick={() => onNavigate({ name: "detail", activityId })}
          className={`flex items-center gap-2 text-sm ${C.dim} hover:text-[var(--tb-fg)] transition-colors mb-8`}
        >
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
            <path d="M9 11L5 7L9 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          {t.backToActivity}
        </button>

        <h1 className={`font-display text-[38px] font-semibold ${C.fg} mb-1`}>{t.uploadMedia}</h1>
        {activity && (
          <p className={`text-sm ${C.dim} mb-10`}>{activity.title}</p>
        )}

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Drop zone */}
          <div
            onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
            onDragLeave={() => setDragOver(false)}
            onDrop={(e) => { e.preventDefault(); setDragOver(false); addFiles(e.dataTransfer.files); }}
            className={`border-2 border-dashed transition-colors ${dragOver ? "border-[#c8893a] bg-[#c8893a]/5" : C.border}`}
          >
            <label className={`flex flex-col items-center gap-3 px-8 py-12 cursor-pointer ${C.dim} hover:text-[var(--tb-fg)] transition-colors`}>
              <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
                <path d="M28 20V26C28 27.1046 27.1046 28 26 28H6C4.89543 28 4 27.1046 4 26V20" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                <path d="M22 11L16 5L10 11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M16 5V21" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              </svg>
              <span className="text-sm font-medium">Drop files here or click to browse</span>
              <span className={`font-mono-data text-[11px] ${C.dimmer}`}>
                Images ≤ 10 MB · Videos ≤ 200 MB · Up to 20 files
              </span>
              <input type="file" multiple accept="image/*,video/*" className="hidden" onChange={(e) => addFiles(e.target.files)} />
            </label>
          </div>

          {/* File list */}
          {files.length > 0 && (
            <div className="space-y-2">
              {files.map((f) => (
                <div key={f.id} className={`flex items-center gap-4 px-4 py-3 border ${C.border} ${C.card}`}>
                  {f.preview ? (
                    <img src={f.preview} alt={f.file.name} className="w-10 h-10 object-cover shrink-0" />
                  ) : (
                    <div className={`w-10 h-10 ${C.secondary} flex items-center justify-center shrink-0`}>
                      <svg width="14" height="16" viewBox="0 0 12 14" fill="none">
                        <path d="M1 1L11 7L1 13V1Z" fill="#c8893a" />
                      </svg>
                    </div>
                  )}
                  <div className="flex-1 min-w-0">
                    <p className={`text-sm ${C.fg} truncate`}>{f.file.name}</p>
                    <p className={`font-mono-data text-[11px] ${C.dim}`}>
                      {formatBytes(f.file.size)} · {f.kind}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => removeFile(f.id)}
                    className={`${C.dim} hover:text-red-400 transition-colors shrink-0`}
                  >
                    <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                      <path d="M10.5 3.5L3.5 10.5M3.5 3.5L10.5 10.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                    </svg>
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* @integration:begin what the server refused, and how far the batch has got */}
          {refusals.length > 0 && (
            <div className={`px-5 py-4 border ${C.border} ${C.card}`}>
              <p className={`text-sm ${C.fg} font-medium`}>{failureText(lang, refusals[0].error).title}</p>
              <ul className="mt-2 space-y-1">
                {refusals.map((r) => (
                  <li key={r.name} className={`text-[13px] ${C.dim} leading-relaxed`}>
                    <span className={C.secondaryFg}>{r.name}</span> — {r.error.message}
                  </li>
                ))}
              </ul>
            </div>
          )}
          {progress.total > 0 && progress.done < progress.total && (
            <p className={`font-mono-data text-[11px] ${C.dim}`}>
              {progress.done} / {progress.total}
            </p>
          )}
          {/* @integration:end */}

          <div className={`flex items-center gap-3 pt-4 border-t ${C.border}`}>
            {/* @integration:begin a batch in flight must not be sent twice */}
            <button
              type="submit"
              disabled={files.length === 0 || (progress.total > 0 && progress.done < progress.total)}
              className="px-6 py-3 bg-[#c8893a] text-[#0f120e] text-sm font-semibold hover:bg-[#d9a050] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              {t.uploadMedia}
              {files.length > 0 && <span className="ml-2 font-mono-data text-[11px]">({files.length})</span>}
            </button>
            {/* @integration:end */}
            <button
              type="button"
              onClick={() => onNavigate({ name: "detail", activityId })}
              className={`px-6 py-3 text-sm ${C.dim} hover:text-[var(--tb-fg)] transition-colors`}
            >
              {t.cancel}
            </button>
          </div>
        </form>
      </div>
    </main>
  );
}

// ─── User Profile ──────────────────────────────────────────────────────────────

function SegmentedControl<T extends string>({
  value, onChange, options,
}: {
  value: T;
  onChange: (v: T) => void;
  options: { value: T; label: string }[];
}) {
  return (
    <div className={`inline-flex border ${C.border} p-0.5 gap-0.5`}>
      {options.map((opt) => (
        <button
          key={opt.value}
          type="button"
          onClick={() => onChange(opt.value)}
          className={`px-4 py-1.5 text-sm font-medium transition-colors ${
            value === opt.value
              ? "bg-[#c8893a] text-[#0f120e]"
              : `${C.dim} hover:text-[var(--tb-fg)]`
          }`}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}

function UserProfile({ authRole, onNavigate }: { authRole: AuthRole; onNavigate: (v: View) => void }) {
  const { theme, lang, setTheme, setLang } = useSettings();
  const t = T[lang];

  // @integration:begin the profile, the avatar and the preferences all live on the server
  const { profile } = useProfile(authRole !== "visitor");

  const [displayName, setDisplayName] = useState("");
  const [bio, setBio] = useState("");
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const [avatarFile, setAvatarFile] = useState<File | null>(null);
  const [avatarRemoved, setAvatarRemoved] = useState(false);
  const [filledFor, setFilledFor] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<ApiError | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Fills the form once, when the profile arrives. The theme and language are the server's too,
  // which is what makes a saved preference survive a reload in a fresh session.
  useEffect(() => {
    if (!profile || filledFor === profile.id) return;
    setFilledFor(profile.id);
    setDisplayName(profile.displayName);
    setBio(profile.description);
    setAvatarPreview(profile.avatarUrl);
    setTheme(profile.theme);
    setLang(profile.lang);
  }, [profile, filledFor, setTheme, setLang]);

  function handleAvatarChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setAvatarFile(file);
    setAvatarRemoved(false);
    const reader = new FileReader();
    reader.onload = () => setAvatarPreview(reader.result as string);
    reader.readAsDataURL(file);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const errs: Record<string, string> = {};
    if (!displayName.trim()) errs.displayName = t.displayNameRequired;
    if (Object.keys(errs).length) { setErrors(errs); return; }

    setSaving(true);
    setSaveError(null);
    try {
      // The avatar is its own verb on its own route, so it goes first and a refusal there
      // leaves the text edits unsaved rather than half-saved.
      if (avatarFile) await uploadAvatar(avatarFile);
      else if (avatarRemoved) await removeAvatar();

      await updateProfile({
        displayName,
        description: bio,
        preferredTheme: theme === "light" ? "Light" : "Dark",
        preferredLanguage: lang,
      });

      setAvatarFile(null);
      setAvatarRemoved(false);
      setSaved(true);
      setTimeout(() => setSaved(false), 2500);
    } catch (failure) {
      const apiError = toApiError(failure);
      const message = fieldError(apiError, "displayName");
      setErrors(message ? { displayName: message } : {});
      setSaveError(message ? null : apiError);
    } finally {
      setSaving(false);
    }
  }
  // @integration:end

  const initials = displayName.trim().split(" ").map((w) => w[0]).join("").slice(0, 2).toUpperCase() || "?";
  const inputClass = (hasError: boolean) =>
    `w-full ${C.card} border px-4 py-3 text-[15px] ${C.fg} placeholder-[var(--tb-dimmer)] focus:outline-none focus:border-[#c8893a] transition-colors ${hasError ? "border-red-700" : C.border}`;

  return (
    <main className="pt-14">
      <div className="max-w-2xl mx-auto px-6 py-12">
        <button onClick={() => onNavigate({ name: "list" })} className={`flex items-center gap-2 text-sm ${C.dim} hover:text-[var(--tb-fg)] transition-colors mb-8`}>
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
            <path d="M9 11L5 7L9 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          {t.backToJournal}
        </button>

        <h1 className={`font-display text-[38px] font-semibold ${C.fg} mb-1`}>{t.profile}</h1>
        <p className={`text-sm ${C.dim} mb-10`}>{t.profileSubtitle}</p>

        <form onSubmit={handleSubmit} className="space-y-8">
          {/* Avatar */}
          <div className="flex items-center gap-6">
            <div className="relative shrink-0">
              <div className={`w-20 h-20 rounded-full ${C.secondary} border-2 ${C.border} overflow-hidden flex items-center justify-center`}>
                {avatarPreview
                  ? <img src={avatarPreview} alt="Avatar" className="w-full h-full object-cover" />
                  : <span className="font-display text-[26px] font-semibold text-[#c8893a]">{initials}</span>}
              </div>
              <label className="absolute -bottom-1 -right-1 w-7 h-7 bg-[#c8893a] flex items-center justify-center cursor-pointer hover:bg-[#d9a050] transition-colors rounded-full">
                <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                  <path d="M6 1V11M1 6H11" stroke="#0f120e" strokeWidth="1.8" strokeLinecap="round" />
                </svg>
                <input type="file" accept="image/*" className="hidden" onChange={handleAvatarChange} />
              </label>
            </div>
            <div>
              <p className={`text-[13px] ${C.fg} font-medium mb-1`}>{t.profilePhoto}</p>
              <p className={`text-[12px] ${C.dim} leading-relaxed`}>{t.photoHint}</p>
              {/* @integration:begin dropping the pick has to drop the file too, so removing an
                  avatar removes it rather than only hiding the preview */}
              {avatarPreview && (
                <button type="button" onClick={() => { setAvatarPreview(null); setAvatarFile(null); setAvatarRemoved(true); }} className={`mt-2 text-[12px] ${C.dim} hover:text-red-400 transition-colors`}>
                  {t.remove}
                </button>
              )}
              {/* @integration:end */}
            </div>
          </div>

          <div className={`border-t ${C.border}`} />

          {/* Display name */}
          <div>
            <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.displayNameLabel}</label>
            <input type="text" value={displayName} onChange={(e) => { setDisplayName(e.target.value); setErrors({}); }} placeholder={t.displayNamePlaceholder} className={inputClass(!!errors.displayName)} />
            {errors.displayName && <p className="mt-1 text-[12px] text-red-400">{errors.displayName}</p>}
          </div>

          {/* Bio */}
          <div>
            <label className={`block font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-2`}>{t.bioLabel}</label>
            <textarea value={bio} onChange={(e) => setBio(e.target.value)} placeholder={t.bioPlaceholder} rows={4} className={`${inputClass(false)} resize-none`} />
            <p className={`mt-1 text-[11px] ${C.dimmer} text-right font-mono-data`}>{bio.length} {t.chars}</p>
          </div>

          <div className={`border-t ${C.border}`} />

          {/* Preferences */}
          <div>
            <h3 className={`font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-5`}>{t.preferences}</h3>
            <div className="space-y-5">
              <div className="flex items-center justify-between">
                <span className={`text-[14px] ${C.secondaryFg}`}>{t.themeLabel}</span>
                <SegmentedControl
                  value={theme}
                  onChange={setTheme}
                  options={[
                    { value: "dark", label: t.themeDark },
                    { value: "light", label: t.themeLight },
                  ]}
                />
              </div>
              <div className="flex items-center justify-between">
                <span className={`text-[14px] ${C.secondaryFg}`}>{t.languageLabel}</span>
                <SegmentedControl
                  value={lang}
                  onChange={setLang}
                  options={[
                    { value: "en", label: "English" },
                    { value: "zh", label: "中文" },
                  ]}
                />
              </div>
            </div>
          </div>

          <div className={`border-t ${C.border}`} />

          {/* Account (read-only) */}
          <div className={`border ${C.border} p-5`}>
            <h3 className={`font-mono-data text-[10px] ${C.dim} uppercase tracking-widest mb-4`}>{t.account}</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <p className={`text-[11px] ${C.dim} mb-1`}>{t.email}</p>
                {/* @integration:begin the address and the role are the token's, not a fixture's */}
                <p className={`text-[14px] ${C.secondaryFg}`}>{profile?.email ?? ""}</p>
                {/* @integration:end */}
              </div>
              <div>
                <p className={`text-[11px] ${C.dim} mb-1`}>{t.role}</p>
                {/* @integration:begin the role is the row the server stores, not a prop */}
                <p className="font-mono-data text-[13px] text-[#c8893a] capitalize">{profile?.role ?? authRole}</p>
                {/* @integration:end */}
              </div>
            </div>

            {/* @integration:begin signing out, which the export has no control for */}
            <div className={`mt-5 pt-4 border-t ${C.border}`}>
              <button type="button" onClick={() => { void signOut(); onNavigate({ name: "list" }); }} className={`text-[13px] ${C.dim} hover:text-red-400 transition-colors`}>
                {signOutText(lang)}
              </button>
            </div>
            {/* @integration:end */}
          </div>

          {/* Save */}
          <div className={`flex items-center gap-4 pt-2 border-t ${C.border}`}>
            {/* @integration:begin a save in flight must not be sent twice */}
            <button type="submit" disabled={saving} className="px-6 py-3 bg-[#c8893a] text-[#0f120e] text-sm font-semibold hover:bg-[#d9a050] disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
              {t.saveBtn}
            </button>
            {/* @integration:end */}
            {saved && (
              <span className={`flex items-center gap-2 text-sm ${C.dim}`}>
                <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                  <path d="M2.5 7L5.5 10L11.5 4" stroke="#c8893a" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                {t.saved}
              </span>
            )}
          </div>

          {/* @integration:begin the refusal the profile form's own validation cannot produce */}
          {saveError && (
            <div className={`px-5 py-4 border ${C.border} ${C.card}`}>
              <p className={`text-sm ${C.fg} font-medium`}>{failureText(lang, saveError).title}</p>
              <p className={`text-[13px] ${C.dim} mt-1 leading-relaxed`}>{failureText(lang, saveError).detail}</p>
            </div>
          )}
          {/* @integration:end */}
        </form>
      </div>
    </main>
  );
}

// ─── App ───────────────────────────────────────────────────────────────────────

export default function App() {
  // @integration:begin who the caller is comes from the sign-in store, not from a constant
  const auth = useAuth();
  const authRole: AuthRole = auth.role;
  // The API's own id for this caller, which is what an activity's `createdByUserId` carries.
  const currentUserId = auth.userId;
  const [view, setView] = useState<View>({ name: "list" });
  const [theme, setTheme] = useState<Theme>("dark");
  const [lang, setLang] = useState<Lang>("en");
  // The footer's count, which the list below does not own.
  const activityCount = useActivityCount();
  // @integration:end

  function handleNavigate(v: View) {
    if ((v.name === "create" || v.name === "edit" || v.name === "upload" || v.name === "profile") && authRole === "visitor") return;
    setView(v);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  const t = T[lang];

  return (
    <SettingsContext.Provider value={{ theme, lang, setTheme, setLang }}>
      <div data-theme={theme} className={`min-h-screen ${C.bg} ${C.fg} transition-colors duration-300`}>
        <Nav authRole={authRole} currentView={view} onNavigate={handleNavigate} />

        {view.name === "list" && <ActivityList authRole={authRole} onNavigate={handleNavigate} />}
        {view.name === "detail" && (
          <ActivityDetail
            activityId={view.activityId}
            authRole={authRole}
            currentUserId={currentUserId}
            onNavigate={handleNavigate}
            onDelete={() => setView({ name: "list" })}
          />
        )}
        {view.name === "create" && <ActivityForm mode="create" onNavigate={handleNavigate} />}
        {view.name === "edit" && <ActivityForm mode="edit" activityId={view.activityId} onNavigate={handleNavigate} />}
        {view.name === "upload" && <MediaUploadForm activityId={view.activityId} onNavigate={handleNavigate} />}
        {view.name === "profile" && <UserProfile authRole={authRole} onNavigate={handleNavigate} />}

        {/* Mobile FAB — shown only on small screens when signed in and not on form pages */}
        {authRole !== "visitor" && view.name !== "create" && view.name !== "edit" && view.name !== "upload" && view.name !== "profile" && (
          <button
            onClick={() => handleNavigate(
              view.name === "detail"
                ? { name: "upload", activityId: view.activityId }
                : { name: "create" }
            )}
            aria-label={view.name === "detail" ? t.uploadMedia : t.newActivity}
            className="sm:hidden fixed bottom-6 right-6 z-40 w-14 h-14 rounded-full bg-[#c8893a] hover:bg-[#d9a050] active:scale-95 transition-all shadow-2xl flex items-center justify-center"
          >
            <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
              <path d="M11 2V20M2 11H20" stroke="#0f120e" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
        )}

        <footer className={`border-t ${C.border} mt-20`}>
          <div className="max-w-6xl mx-auto px-6 py-6 flex items-center justify-between">
            <span className={`font-display text-[13px] ${C.dimmer}`}>TrailBlaze</span>
            {/* @integration:begin the count is the server's, not a fixture's length */}
            <span className={`font-mono-data text-[11px] ${C.dimmer}`}>{t.footerTagline(activityCount)}</span>
            {/* @integration:end */}
          </div>
        </footer>
      </div>
    </SettingsContext.Provider>
  );
}
