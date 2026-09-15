import { useState } from "react";

type AuthRole = "visitor" | "user" | "admin";

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
}

interface MediaItem {
  id: string;
  kind: "Image" | "Video";
  originalFileName: string;
  sizeBytes: number;
  url: string;
  contentType: string;
}

type View =
  | { name: "list" }
  | { name: "detail"; activityId: string }
  | { name: "create" }
  | { name: "edit"; activityId: string };

const MOCK_ACTIVITIES: Activity[] = [
  {
    id: "1",
    title: "Summit Attempt on Mount Rainier",
    location: "Mount Rainier National Park, WA",
    activityDate: "2026-09-12",
    description:
      "Set out from Paradise at 0300 with crampon packs and a clear forecast. Hit the Disappointment Cleaver in whiteout conditions by 0900 — turned back at 12,400 ft. Not today, but the mountain is patient.",
    coverImageUrl:
      "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?w=800&h=500&fit=crop&auto=format",
    createdBy: "Rhys Caldwell",
    createdByUserId: "user-1",
    mediaCount: 12,
  },
  {
    id: "2",
    title: "Enchantments Through-Hike",
    location: "Alpine Lakes Wilderness, WA",
    activityDate: "2026-09-05",
    description:
      "Three days, 18 miles, elevation change that makes your legs ask hard questions. The Upper Enchantments at dawn — no words. Camped at Leprechaun Lake on night two. Permit lottery finally came through after four years of applying.",
    coverImageUrl:
      "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?w=800&h=500&fit=crop&auto=format",
    createdBy: "Saoirse Mäkinen",
    createdByUserId: "user-2",
    mediaCount: 28,
  },
  {
    id: "3",
    title: "Night Run on the PCT",
    location: "Snoqualmie Pass, WA",
    activityDate: "2026-08-29",
    description:
      "Full moon, headlamp as backup only. 22 miles north from the pass and back. Hit a black bear and her cub at mile 9 — gave them wide berth, continued. The trail at 2 AM has a different texture.",
    coverImageUrl:
      "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?w=800&h=500&fit=crop&auto=format",
    createdBy: "Rhys Caldwell",
    createdByUserId: "user-1",
    mediaCount: 4,
  },
  {
    id: "4",
    title: "Glacier Crossing — Eldorado Peak",
    location: "North Cascades, WA",
    activityDate: "2026-08-17",
    description:
      "Approached via Roush Creek trail. Roped up on the glacier, navigated crevasse field in early morning freeze. Summit at 0745. Views east into the Cascades for 200 miles.",
    coverImageUrl:
      "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=800&h=500&fit=crop&auto=format",
    createdBy: "Tomás Herrera",
    createdByUserId: "user-3",
    mediaCount: 19,
  },
  {
    id: "5",
    title: "Olympic Coast Packraft",
    location: "Olympic Peninsula, WA",
    activityDate: "2026-08-03",
    description:
      "Four days paddling and beach-camping from Rialto Beach to Oil City. Timed the headlands on the tides. Found a massive grey whale skeleton at Cape Johnson. Zero other humans from day two on.",
    coverImageUrl:
      "https://images.unsplash.com/photo-1505118380757-91f5f5632de0?w=800&h=500&fit=crop&auto=format",
    createdBy: "Saoirse Mäkinen",
    createdByUserId: "user-2",
    mediaCount: 33,
  },
  {
    id: "6",
    title: "Ptarmigan Traverse",
    location: "North Cascades Wilderness, WA",
    activityDate: "2026-07-20",
    description:
      "Classic route. Six days, primarily on snow and glacier until late season. Caught a massive storm on day four and bivy'd in a crevasse lip. Emerged to complete the traverse in excellent style.",
    coverImageUrl:
      "https://images.unsplash.com/photo-1519681393784-d120267933ba?w=800&h=500&fit=crop&auto=format",
    createdBy: "Tomás Herrera",
    createdByUserId: "user-3",
    mediaCount: 41,
  },
];

const MOCK_MEDIA: MediaItem[] = [
  {
    id: "m1",
    kind: "Image",
    originalFileName: "summit_approach.jpg",
    sizeBytes: 4200000,
    contentType: "image/jpeg",
    url: "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?w=600&h=400&fit=crop&auto=format",
  },
  {
    id: "m2",
    kind: "Image",
    originalFileName: "cleaver_camp.jpg",
    sizeBytes: 3800000,
    contentType: "image/jpeg",
    url: "https://images.unsplash.com/photo-1519681393784-d120267933ba?w=600&h=400&fit=crop&auto=format",
  },
  {
    id: "m3",
    kind: "Image",
    originalFileName: "rope_team.jpg",
    sizeBytes: 5100000,
    contentType: "image/jpeg",
    url: "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop&auto=format",
  },
  {
    id: "m4",
    kind: "Video",
    originalFileName: "whiteout_conditions.mov",
    sizeBytes: 48000000,
    contentType: "video/quicktime",
    url: "",
  },
  {
    id: "m5",
    kind: "Image",
    originalFileName: "glacier_crevasse.jpg",
    sizeBytes: 3200000,
    contentType: "image/jpeg",
    url: "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?w=600&h=400&fit=crop&auto=format",
  },
  {
    id: "m6",
    kind: "Image",
    originalFileName: "view_east.jpg",
    sizeBytes: 6700000,
    contentType: "image/jpeg",
    url: "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?w=600&h=400&fit=crop&auto=format",
  },
];

function formatDate(dateStr: string) {
  const d = new Date(dateStr + "T12:00:00");
  return d.toLocaleDateString("en-US", {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function formatBytes(bytes: number) {
  if (bytes >= 1e6) return (bytes / 1e6).toFixed(1) + " MB";
  return (bytes / 1e3).toFixed(0) + " KB";
}

function Nav({
  authRole,
  onNavigate,
}: {
  authRole: AuthRole;
  onNavigate: (v: View) => void;
}) {
  return (
    <header className="fixed top-0 left-0 right-0 z-50 border-b border-[#2a2f26] bg-[#0f120e]/95 backdrop-blur-sm">
      <div className="max-w-6xl mx-auto px-6 h-14 flex items-center justify-between">
        <button
          onClick={() => onNavigate({ name: "list" })}
          className="flex items-center gap-3 group"
        >
          <span className="text-[#c8893a] text-lg leading-none">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
              <path
                d="M10 2L13 7H17L14 11L15.5 16L10 13L4.5 16L6 11L3 7H7L10 2Z"
                fill="currentColor"
                opacity="0.9"
              />
            </svg>
          </span>
          <span className="font-display font-semibold text-[15px] tracking-wide text-[#e8e3d8] group-hover:text-[#c8893a] transition-colors">
            TrailBlaze
          </span>
        </button>

        <div className="flex items-center gap-4">
          {authRole !== "visitor" && (
            <button
              onClick={() => onNavigate({ name: "create" })}
              className="hidden sm:flex items-center gap-2 text-sm font-medium text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
            >
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path
                  d="M7 1V13M1 7H13"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                />
              </svg>
              New Activity
            </button>
          )}

          {authRole === "visitor" ? (
            <a
              href="/auth/login"
              className="flex items-center gap-2 text-sm font-medium text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
            >
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path
                  d="M9 2H12C12.5523 2 13 2.44772 13 3V11C13 11.5523 12.5523 12 12 12H9"
                  stroke="currentColor"
                  strokeWidth="1.2"
                  strokeLinecap="round"
                />
                <path
                  d="M6 9.5L9 7L6 4.5"
                  stroke="currentColor"
                  strokeWidth="1.2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
                <path
                  d="M1 7H9"
                  stroke="currentColor"
                  strokeWidth="1.2"
                  strokeLinecap="round"
                />
              </svg>
              Sign in with Microsoft
            </a>
          ) : (
            <div className="flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-[#c8893a] flex items-center justify-center text-[10px] font-semibold text-[#0f120e]">
                {authRole === "admin" ? "A" : "U"}
              </span>
              <span className="hidden sm:block text-sm text-[#b8b2a4] capitalize">
                {authRole}
              </span>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}

function ActivityCard({
  activity,
  onClick,
}: {
  activity: Activity;
  onClick: () => void;
}) {
  return (
    <article
      className="group cursor-pointer border border-[#2a2f26] hover:border-[#3a4036] transition-all duration-300 bg-[#161a14] hover:bg-[#191d17]"
      onClick={onClick}
    >
      <div className="aspect-[16/9] overflow-hidden bg-[#1a1e18] relative">
        {activity.coverImageUrl ? (
          <img
            src={activity.coverImageUrl}
            alt={activity.title}
            className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            <svg
              width="32"
              height="32"
              viewBox="0 0 32 32"
              fill="none"
              className="opacity-20"
            >
              <path
                d="M4 24L11 14L16 20L21 16L28 24H4Z"
                stroke="#e8e3d8"
                strokeWidth="1.5"
                fill="none"
              />
            </svg>
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-[#161a14]/60 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
      </div>

      <div className="p-5">
        <div className="flex items-center gap-3 mb-3">
          <span className="font-mono-data text-[11px] text-[#c8893a] tracking-wider">
            {formatDate(activity.activityDate)}
          </span>
          <span className="w-1 h-1 rounded-full bg-[#2a2f26]" />
          <span className="text-[11px] text-[#7a7568] truncate">
            {activity.location}
          </span>
        </div>

        <h2 className="font-display font-semibold text-[18px] leading-snug text-[#e8e3d8] group-hover:text-white transition-colors mb-2 line-clamp-2">
          {activity.title}
        </h2>

        <p className="text-sm text-[#7a7568] line-clamp-2 leading-relaxed mb-4">
          {activity.description}
        </p>

        <div className="flex items-center justify-between pt-3 border-t border-[#2a2f26]">
          <span className="text-[12px] text-[#7a7568]">{activity.createdBy}</span>
          <span className="font-mono-data text-[11px] text-[#3a4036] tracking-wider">
            {activity.mediaCount} media
          </span>
        </div>
      </div>
    </article>
  );
}

function ActivityList({
  authRole,
  onNavigate,
}: {
  authRole: AuthRole;
  onNavigate: (v: View) => void;
}) {
  const [page, setPage] = useState(1);
  const pageSize = 6;
  const total = MOCK_ACTIVITIES.length;
  const totalPages = Math.ceil(total / pageSize);
  const paged = MOCK_ACTIVITIES.slice((page - 1) * pageSize, page * pageSize);

  return (
    <main className="pt-14">
      <div className="max-w-6xl mx-auto px-6">
        {/* Hero bar */}
        <div className="py-16 border-b border-[#2a2f26] mb-12">
          <div className="grid grid-cols-1 lg:grid-cols-[1fr_auto] gap-8 items-end">
            <div>
              <p className="font-mono-data text-[11px] text-[#c8893a] tracking-widest uppercase mb-4">
                Shared Journal
              </p>
              <h1 className="font-display text-[52px] lg:text-[68px] leading-[0.95] font-light text-[#e8e3d8]">
                Every trail,
                <br />
                <em className="font-light not-italic text-[#c8893a]">one record.</em>
              </h1>
              <p className="mt-5 text-[15px] text-[#7a7568] max-w-md leading-relaxed">
                A communal log of routes taken, summits chased, and coastlines walked.
                Public by default — media for those who sign in.
              </p>
            </div>
            <div className="lg:text-right">
              <div className="inline-flex flex-col gap-1">
                <span className="font-mono-data text-[36px] font-medium text-[#e8e3d8]">
                  {total}
                </span>
                <span className="text-[12px] text-[#7a7568] uppercase tracking-widest">
                  activities logged
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Auth callout for visitors */}
        {authRole === "visitor" && (
          <div className="mb-10 flex items-start gap-4 px-5 py-4 border border-[#2a2f26] bg-[#161a14]">
            <svg
              width="16"
              height="16"
              viewBox="0 0 16 16"
              fill="none"
              className="mt-0.5 shrink-0 text-[#c8893a]"
            >
              <circle cx="8" cy="8" r="7" stroke="currentColor" strokeWidth="1.2" />
              <path d="M8 7V11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              <circle cx="8" cy="5" r="0.75" fill="currentColor" />
            </svg>
            <p className="text-sm text-[#b8b2a4]">
              Sign in to view photos and videos, and to add your own activities to the journal.
            </p>
          </div>
        )}

        {/* Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-px bg-[#2a2f26]">
          {paged.map((a) => (
            <div key={a.id} className="bg-[#0f120e]">
              <ActivityCard
                activity={a}
                onClick={() => onNavigate({ name: "detail", activityId: a.id })}
              />
            </div>
          ))}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between py-10 border-t border-[#2a2f26] mt-px">
            <button
              disabled={page === 1}
              onClick={() => setPage(page - 1)}
              className="flex items-center gap-2 text-sm text-[#7a7568] hover:text-[#e8e3d8] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path
                  d="M10 12L6 8L10 4"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
              Previous
            </button>
            <span className="font-mono-data text-[12px] text-[#7a7568]">
              {page} / {totalPages}
            </span>
            <button
              disabled={page === totalPages}
              onClick={() => setPage(page + 1)}
              className="flex items-center gap-2 text-sm text-[#7a7568] hover:text-[#e8e3d8] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            >
              Next
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path
                  d="M6 4L10 8L6 12"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </button>
          </div>
        )}
      </div>
    </main>
  );
}

function ActivityDetail({
  activityId,
  authRole,
  currentUserId,
  onNavigate,
  onDelete,
}: {
  activityId: string;
  authRole: AuthRole;
  currentUserId: string;
  onNavigate: (v: View) => void;
  onDelete: (id: string) => void;
}) {
  const activity = MOCK_ACTIVITIES.find((a) => a.id === activityId);
  const [mediaLoaded, setMediaLoaded] = useState(false);
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  if (!activity) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <p className="text-[#7a7568]">Activity not found.</p>
      </main>
    );
  }

  const isOwner = activity.createdByUserId === currentUserId;
  const canEdit = authRole === "admin" || (authRole === "user" && isOwner);
  const canViewMedia = authRole !== "visitor";
  const media = canViewMedia && mediaLoaded ? MOCK_MEDIA : [];
  const imageMedia = media.filter((m) => m.kind === "Image");

  return (
    <main className="pt-14">
      {/* Cover hero */}
      <div className="relative h-[55vh] min-h-[320px] bg-[#1a1e18]">
        {activity.coverImageUrl && (
          <img
            src={activity.coverImageUrl}
            alt={activity.title}
            className="absolute inset-0 w-full h-full object-cover"
          />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-[#0f120e] via-[#0f120e]/40 to-transparent" />

        <div className="absolute bottom-0 left-0 right-0 max-w-6xl mx-auto px-6 pb-10">
          <button
            onClick={() => onNavigate({ name: "list" })}
            className="flex items-center gap-2 text-sm text-[#b8b2a4] hover:text-[#e8e3d8] transition-colors mb-6"
          >
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
              <path
                d="M9 11L5 7L9 3"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
            All activities
          </button>

          <div className="flex items-end justify-between gap-4 flex-wrap">
            <div>
              <div className="flex items-center gap-3 mb-3">
                <span className="font-mono-data text-[11px] text-[#c8893a] tracking-wider">
                  {formatDate(activity.activityDate)}
                </span>
                <span className="text-[#2a2f26]">·</span>
                <span className="text-[12px] text-[#b8b2a4]">{activity.location}</span>
              </div>
              <h1 className="font-display font-semibold text-[36px] lg:text-[48px] leading-tight text-white max-w-3xl">
                {activity.title}
              </h1>
            </div>

            {canEdit && (
              <div className="flex items-center gap-2 shrink-0">
                <button
                  onClick={() => onNavigate({ name: "edit", activityId: activity.id })}
                  className="px-4 py-2 text-sm font-medium text-[#e8e3d8] border border-[#2a2f26] hover:border-[#c8893a] hover:text-[#c8893a] transition-colors"
                >
                  Edit
                </button>
                {!confirmDelete ? (
                  <button
                    onClick={() => setConfirmDelete(true)}
                    className="px-4 py-2 text-sm font-medium text-[#7a7568] border border-[#2a2f26] hover:border-red-800 hover:text-red-400 transition-colors"
                  >
                    Delete
                  </button>
                ) : (
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => onDelete(activity.id)}
                      className="px-3 py-2 text-sm font-medium text-red-400 border border-red-800 hover:bg-red-950 transition-colors"
                    >
                      Confirm
                    </button>
                    <button
                      onClick={() => setConfirmDelete(false)}
                      className="px-3 py-2 text-sm text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
                    >
                      Cancel
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Body */}
      <div className="max-w-6xl mx-auto px-6 py-12">
        <div className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-12">
          {/* Main content */}
          <div>
            {activity.description && (
              <p className="text-[16px] text-[#b8b2a4] leading-[1.8] mb-10">
                {activity.description}
              </p>
            )}

            {/* Media section */}
            <section>
              <div className="flex items-center justify-between mb-6">
                <h2 className="font-display font-semibold text-[22px] text-[#e8e3d8]">
                  Photos & Videos
                </h2>
                {canViewMedia && !mediaLoaded && (
                  <button
                    onClick={() => setMediaLoaded(true)}
                    className="flex items-center gap-2 text-sm text-[#c8893a] hover:text-[#d9a050] transition-colors"
                  >
                    Load media
                    <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                      <path
                        d="M5 3L9 7L5 11"
                        stroke="currentColor"
                        strokeWidth="1.5"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                    </svg>
                  </button>
                )}
              </div>

              {authRole === "visitor" ? (
                <div className="border border-dashed border-[#2a2f26] px-8 py-12 text-center">
                  <div className="w-10 h-10 mx-auto mb-4 flex items-center justify-center border border-[#2a2f26]">
                    <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
                      <rect
                        x="3"
                        y="7"
                        width="12"
                        height="9"
                        rx="1"
                        stroke="#7a7568"
                        strokeWidth="1.2"
                      />
                      <path
                        d="M6 7V5C6 3.34315 7.34315 2 9 2C10.6569 2 12 3.34315 12 5V7"
                        stroke="#7a7568"
                        strokeWidth="1.2"
                      />
                    </svg>
                  </div>
                  <p className="text-[14px] text-[#7a7568]">
                    Sign in to view {activity.mediaCount} photos and videos
                  </p>
                </div>
              ) : !mediaLoaded ? (
                <div className="border border-dashed border-[#2a2f26] px-8 py-12 text-center">
                  <p className="text-[14px] text-[#7a7568]">
                    {activity.mediaCount} items — click "Load media" to fetch
                  </p>
                </div>
              ) : (
                <div>
                  {imageMedia.length > 0 && (
                    <div className="grid grid-cols-2 md:grid-cols-3 gap-1 mb-4">
                      {imageMedia.map((m, i) => (
                        <button
                          key={m.id}
                          className="aspect-square overflow-hidden bg-[#1a1e18] group relative"
                          onClick={() => setLightboxIndex(i)}
                        >
                          <img
                            src={m.url}
                            alt={m.originalFileName}
                            className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105"
                          />
                          <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors" />
                        </button>
                      ))}
                    </div>
                  )}
                  {media
                    .filter((m) => m.kind === "Video")
                    .map((m) => (
                      <div
                        key={m.id}
                        className="flex items-center gap-4 px-4 py-3 border border-[#2a2f26] mb-2"
                      >
                        <div className="w-8 h-8 bg-[#1e2419] flex items-center justify-center shrink-0">
                          <svg width="12" height="14" viewBox="0 0 12 14" fill="none">
                            <path d="M1 1L11 7L1 13V1Z" fill="#c8893a" />
                          </svg>
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm text-[#e8e3d8] truncate">
                            {m.originalFileName}
                          </p>
                          <p className="font-mono-data text-[11px] text-[#7a7568]">
                            {formatBytes(m.sizeBytes)} · {m.contentType}
                          </p>
                        </div>
                        <span className="font-mono-data text-[10px] text-[#3a4036] uppercase tracking-wider">
                          SAS required
                        </span>
                      </div>
                    ))}
                </div>
              )}
            </section>

            {/* Add media (owners/admin) */}
            {canEdit && mediaLoaded && (
              <div className="mt-6 border border-dashed border-[#2a2f26] hover:border-[#c8893a]/40 transition-colors">
                <button className="w-full px-6 py-5 flex items-center gap-3 text-sm text-[#7a7568] hover:text-[#e8e3d8] transition-colors">
                  <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                    <path
                      d="M8 1V15M1 8H15"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                    />
                  </svg>
                  Upload photos or videos
                </button>
              </div>
            )}
          </div>

          {/* Sidebar */}
          <aside className="space-y-0">
            <div className="border border-[#2a2f26] p-5">
              <h3 className="font-mono-data text-[10px] text-[#7a7568] uppercase tracking-widest mb-4">
                Details
              </h3>
              <dl className="space-y-4">
                <div>
                  <dt className="text-[11px] text-[#7a7568] mb-1">Date</dt>
                  <dd className="text-[14px] text-[#e8e3d8]">
                    {formatDate(activity.activityDate)}
                  </dd>
                </div>
                <div>
                  <dt className="text-[11px] text-[#7a7568] mb-1">Location</dt>
                  <dd className="text-[14px] text-[#e8e3d8]">{activity.location}</dd>
                </div>
                <div>
                  <dt className="text-[11px] text-[#7a7568] mb-1">Logged by</dt>
                  <dd className="text-[14px] text-[#e8e3d8]">{activity.createdBy}</dd>
                </div>
                <div>
                  <dt className="text-[11px] text-[#7a7568] mb-1">Media</dt>
                  <dd className="font-mono-data text-[14px] text-[#e8e3d8]">
                    {activity.mediaCount} items
                  </dd>
                </div>
              </dl>
            </div>
          </aside>
        </div>
      </div>

      {/* Lightbox */}
      {lightboxIndex !== null && (
        <div
          className="fixed inset-0 z-50 bg-black/95 flex items-center justify-center"
          onClick={() => setLightboxIndex(null)}
        >
          <button
            className="absolute top-4 right-4 text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
            onClick={() => setLightboxIndex(null)}
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path
                d="M18 6L6 18M6 6L18 18"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
              />
            </svg>
          </button>
          <button
            className="absolute left-4 top-1/2 -translate-y-1/2 text-[#7a7568] hover:text-[#e8e3d8] transition-colors p-2"
            onClick={(e) => {
              e.stopPropagation();
              setLightboxIndex((prev) =>
                prev !== null ? (prev - 1 + imageMedia.length) % imageMedia.length : 0
              );
            }}
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path
                d="M15 18L9 12L15 6"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
          <img
            src={imageMedia[lightboxIndex]?.url}
            alt={imageMedia[lightboxIndex]?.originalFileName}
            className="max-w-4xl max-h-[85vh] object-contain"
            onClick={(e) => e.stopPropagation()}
          />
          <button
            className="absolute right-4 top-1/2 -translate-y-1/2 text-[#7a7568] hover:text-[#e8e3d8] transition-colors p-2"
            onClick={(e) => {
              e.stopPropagation();
              setLightboxIndex((prev) =>
                prev !== null ? (prev + 1) % imageMedia.length : 0
              );
            }}
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path
                d="M9 18L15 12L9 6"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
          <div className="absolute bottom-4 left-1/2 -translate-x-1/2 font-mono-data text-[11px] text-[#7a7568]">
            {lightboxIndex + 1} / {imageMedia.length}
          </div>
        </div>
      )}
    </main>
  );
}

function ActivityForm({
  mode,
  activityId,
  onNavigate,
}: {
  mode: "create" | "edit";
  activityId?: string;
  onNavigate: (v: View) => void;
}) {
  const existing = activityId
    ? MOCK_ACTIVITIES.find((a) => a.id === activityId)
    : null;

  const [form, setForm] = useState({
    title: existing?.title ?? "",
    location: existing?.location ?? "",
    activityDate: existing?.activityDate ?? "",
    description: existing?.description ?? "",
  });
  const [coverPreview, setCoverPreview] = useState<string | null>(
    existing?.coverImageUrl ?? null
  );
  const [submitted, setSubmitted] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  function validate() {
    const e: Record<string, string> = {};
    if (!form.title.trim()) e.title = "Title is required";
    if (!form.location.trim()) e.location = "Location is required";
    if (!form.activityDate) e.activityDate = "Date is required";
    return e;
  }

  function handleSubmit(evt: React.FormEvent) {
    evt.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length) {
      setErrors(errs);
      return;
    }
    setSubmitted(true);
    setTimeout(() => onNavigate({ name: "list" }), 1400);
  }

  if (submitted) {
    return (
      <main className="pt-14 min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 border border-[#c8893a] flex items-center justify-center mx-auto mb-4">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
              <path
                d="M4 10L8 14L16 6"
                stroke="#c8893a"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          <p className="font-display text-[22px] text-[#e8e3d8]">
            {mode === "create" ? "Activity logged." : "Changes saved."}
          </p>
          <p className="text-sm text-[#7a7568] mt-1">Returning to journal…</p>
        </div>
      </main>
    );
  }

  return (
    <main className="pt-14">
      <div className="max-w-3xl mx-auto px-6 py-12">
        <button
          onClick={() =>
            onNavigate(
              activityId ? { name: "detail", activityId } : { name: "list" }
            )
          }
          className="flex items-center gap-2 text-sm text-[#7a7568] hover:text-[#e8e3d8] transition-colors mb-8"
        >
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
            <path
              d="M9 11L5 7L9 3"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          {mode === "create" ? "Back to journal" : "Back to activity"}
        </button>

        <h1 className="font-display text-[38px] font-semibold text-[#e8e3d8] mb-8">
          {mode === "create" ? "Log an activity" : "Edit activity"}
        </h1>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Title */}
          <div>
            <label className="block font-mono-data text-[10px] text-[#7a7568] uppercase tracking-widest mb-2">
              Title *
            </label>
            <input
              type="text"
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              placeholder="Summit attempt on Glacier Peak"
              className={`w-full bg-[#161a14] border px-4 py-3 text-[15px] text-[#e8e3d8] placeholder-[#3a4036] focus:outline-none focus:border-[#c8893a] transition-colors ${
                errors.title ? "border-red-700" : "border-[#2a2f26]"
              }`}
            />
            {errors.title && (
              <p className="mt-1 text-[12px] text-red-400">{errors.title}</p>
            )}
          </div>

          {/* Date + Location row */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block font-mono-data text-[10px] text-[#7a7568] uppercase tracking-widest mb-2">
                Activity date *
              </label>
              <input
                type="date"
                value={form.activityDate}
                onChange={(e) => setForm({ ...form, activityDate: e.target.value })}
                className={`w-full bg-[#161a14] border px-4 py-3 text-[15px] text-[#e8e3d8] focus:outline-none focus:border-[#c8893a] transition-colors [color-scheme:dark] ${
                  errors.activityDate ? "border-red-700" : "border-[#2a2f26]"
                }`}
              />
              {errors.activityDate && (
                <p className="mt-1 text-[12px] text-red-400">{errors.activityDate}</p>
              )}
            </div>
            <div>
              <label className="block font-mono-data text-[10px] text-[#7a7568] uppercase tracking-widest mb-2">
                Location *
              </label>
              <input
                type="text"
                value={form.location}
                onChange={(e) => setForm({ ...form, location: e.target.value })}
                placeholder="Mount Rainier National Park, WA"
                className={`w-full bg-[#161a14] border px-4 py-3 text-[15px] text-[#e8e3d8] placeholder-[#3a4036] focus:outline-none focus:border-[#c8893a] transition-colors ${
                  errors.location ? "border-red-700" : "border-[#2a2f26]"
                }`}
              />
              {errors.location && (
                <p className="mt-1 text-[12px] text-red-400">{errors.location}</p>
              )}
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="block font-mono-data text-[10px] text-[#7a7568] uppercase tracking-widest mb-2">
              Description
            </label>
            <textarea
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              placeholder="What happened out there…"
              rows={5}
              className="w-full bg-[#161a14] border border-[#2a2f26] px-4 py-3 text-[15px] text-[#e8e3d8] placeholder-[#3a4036] focus:outline-none focus:border-[#c8893a] transition-colors resize-none"
            />
          </div>

          {/* Cover image */}
          <div>
            <label className="block font-mono-data text-[10px] text-[#7a7568] uppercase tracking-widest mb-2">
              Cover image
            </label>
            <div className="border border-dashed border-[#2a2f26] hover:border-[#c8893a]/40 transition-colors">
              {coverPreview ? (
                <div className="relative">
                  <img
                    src={coverPreview}
                    alt="Cover preview"
                    className="w-full h-40 object-cover"
                  />
                  <button
                    type="button"
                    onClick={() => setCoverPreview(null)}
                    className="absolute top-2 right-2 w-7 h-7 bg-[#0f120e]/80 flex items-center justify-center text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
                  >
                    <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                      <path
                        d="M9 3L3 9M3 3L9 9"
                        stroke="currentColor"
                        strokeWidth="1.5"
                        strokeLinecap="round"
                      />
                    </svg>
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  className="w-full px-6 py-8 flex flex-col items-center gap-2 text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
                >
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
                    <path
                      d="M21 15V19C21 20.1046 20.1046 21 19 21H5C3.89543 21 3 20.1046 3 19V15"
                      stroke="currentColor"
                      strokeWidth="1.2"
                      strokeLinecap="round"
                    />
                    <path
                      d="M17 8L12 3L7 8"
                      stroke="currentColor"
                      strokeWidth="1.2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                    <path
                      d="M12 3V15"
                      stroke="currentColor"
                      strokeWidth="1.2"
                      strokeLinecap="round"
                    />
                  </svg>
                  <span className="text-sm">Upload cover image</span>
                  <span className="font-mono-data text-[11px] text-[#3a4036]">
                    Public · any size · goes to public container
                  </span>
                </button>
              )}
            </div>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-3 pt-4 border-t border-[#2a2f26]">
            <button
              type="submit"
              className="px-6 py-3 bg-[#c8893a] text-[#0f120e] text-sm font-semibold hover:bg-[#d9a050] transition-colors"
            >
              {mode === "create" ? "Log activity" : "Save changes"}
            </button>
            <button
              type="button"
              onClick={() =>
                onNavigate(
                  activityId ? { name: "detail", activityId } : { name: "list" }
                )
              }
              className="px-6 py-3 text-sm text-[#7a7568] hover:text-[#e8e3d8] transition-colors"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </main>
  );
}

export default function App() {
  const [authRole, setAuthRole] = useState<AuthRole>("user");
  const [view, setView] = useState<View>({ name: "list" });

  const currentUserId = authRole === "user" ? "user-1" : authRole === "admin" ? "admin-1" : "";

  function handleNavigate(v: View) {
    if ((v.name === "create" || v.name === "edit") && authRole === "visitor") return;
    setView(v);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function handleDelete(_id: string) {
    setView({ name: "list" });
  }

  return (
    <div className="min-h-screen bg-[#0f120e]">
      <Nav authRole={authRole} onNavigate={handleNavigate} />

      {view.name === "list" && (
        <ActivityList authRole={authRole} onNavigate={handleNavigate} />
      )}
      {view.name === "detail" && (
        <ActivityDetail
          activityId={view.activityId}
          authRole={authRole}
          currentUserId={currentUserId}
          onNavigate={handleNavigate}
          onDelete={handleDelete}
        />
      )}
      {view.name === "create" && (
        <ActivityForm mode="create" onNavigate={handleNavigate} />
      )}
      {view.name === "edit" && (
        <ActivityForm
          mode="edit"
          activityId={view.activityId}
          onNavigate={handleNavigate}
        />
      )}

      <footer className="border-t border-[#2a2f26] mt-20">
        <div className="max-w-6xl mx-auto px-6 py-6 flex items-center justify-between">
          <span className="font-display text-[13px] text-[#3a4036]">TrailBlaze</span>
          <span className="font-mono-data text-[11px] text-[#3a4036]">
            One shared journal · {MOCK_ACTIVITIES.length} activities
          </span>
        </div>
      </footer>
    </div>
  );
}
