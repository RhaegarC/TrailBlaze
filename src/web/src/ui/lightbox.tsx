import { useEffect, useRef, useState, type TouchEvent } from "react";

/** Horizontal travel, in px, that commits a swipe when the finger lifts. */
const COMMIT_DISTANCE = 60;
/** Horizontal speed, in px/ms, that commits a swipe even where the finger barely travelled. */
const COMMIT_SPEED = 0.4;
/** How long the strip takes to slide one image across, in ms. */
const SLIDE_MS = 240;
/** Decelerating, so the strip leaves the finger quickly and arrives gently. */
const EASING = "cubic-bezier(0.22, 0.61, 0.36, 1)";

export interface LightboxImage {
  id: string;
  url: string;
  originalFileName: string;
}

interface LightboxProps {
  images: LightboxImage[];
  index: number;
  onIndexChange: (index: number) => void;
  onClose: () => void;
}

/**
 * A full-screen viewer holding the image, its predecessor and its successor in one strip, which a
 * finger drags and the arrows step.
 */
export function Lightbox({ images, index, onIndexChange, onClose }: LightboxProps) {
  const track = useRef<HTMLDivElement>(null);
  const settle = useRef<number | null>(null);
  const gesture = useRef<{ x: number; y: number; at: number; width: number; along: boolean | null } | null>(null);
  const dragged = useRef(false);
  const [offset, setOffset] = useState(0);
  // The strip only carries a transition while settling, so that a drag tracks the finger exactly.
  const [settling, setSettling] = useState(false);

  const count = images.length;
  const at = (step: number) => (index + step + count) % count;

  useEffect(() => () => { if (settle.current !== null) window.clearTimeout(settle.current); }, []);

  /** Slide `step` images across — or 0 to let the strip fall back — then hand the new index up. */
  function glide(step: number): void {
    if (settle.current !== null) window.clearTimeout(settle.current);
    setSettling(true);
    setOffset(-step * (track.current?.offsetWidth ?? 0));
    settle.current = window.setTimeout(() => {
      settle.current = null;
      if (step !== 0) onIndexChange(at(step));
      // The new index moves every layer one width along and the offset gives that width straight
      // back, so this pair lands the strip exactly where the slide left it — but only if both are
      // applied in one commit, which is why they are set together and only after the slide is done.
      setSettling(false);
      setOffset(0);
    }, SLIDE_MS);
  }

  function onTouchStart(event: TouchEvent): void {
    if (settle.current !== null) window.clearTimeout(settle.current);
    settle.current = null;
    const touch = event.touches[0];
    gesture.current = { x: touch.clientX, y: touch.clientY, at: event.timeStamp, width: track.current?.offsetWidth ?? 0, along: null };
    dragged.current = false;
    setSettling(false);
    setOffset(0);
  }

  function onTouchMove(event: TouchEvent): void {
    const from = gesture.current;
    if (!from) return;
    const touch = event.touches[0];
    const dx = touch.clientX - from.x;
    const dy = touch.clientY - from.y;

    if (from.along === null) {
      // Hold until the drag declares an axis, so a vertical drag is never taken from the browser.
      if (Math.abs(dx) < 8 && Math.abs(dy) < 8) return;
      from.along = Math.abs(dx) > Math.abs(dy);
      if (from.along) dragged.current = true;
    }
    if (!from.along) return;

    setSettling(false);
    setOffset(dx);
  }

  function onTouchEnd(event: TouchEvent): void {
    const from = gesture.current;
    gesture.current = null;
    if (!from) return;
    if (from.along !== true) return;
    // One image has nowhere to go, but a drag still has to be undone.
    if (count < 2) { glide(0); return; }

    const touch = event.changedTouches[0];
    const dx = touch.clientX - from.x;
    const speed = Math.abs(dx) / Math.max(1, event.timeStamp - from.at);
    const committed = Math.abs(dx) > COMMIT_DISTANCE || speed > COMMIT_SPEED;
    // A finger travelling left pulls the next image in, the way a page turns.
    glide(committed ? (dx < 0 ? 1 : -1) : 0);
  }

  return (
    <div
      className="fixed inset-0 z-50 bg-black/95 select-none touch-pan-y"
      onTouchStart={onTouchStart}
      onTouchMove={onTouchMove}
      onTouchEnd={onTouchEnd}
      onTouchCancel={() => { gesture.current = null; setSettling(false); setOffset(0); }}
      onClick={() => { if (dragged.current) { dragged.current = false; return; } onClose(); }}
    >
      <div
        ref={track}
        className="absolute inset-0 overflow-hidden"
        style={{ transform: `translateX(${offset}px)`, transition: settling ? `transform ${SLIDE_MS}ms ${EASING}` : "none" }}
      >
        {[-1, 0, 1].map((slot) => {
          const image = images[at(slot)];
          if (!image) return null;
          return (
            <div key={slot} className="absolute inset-0 flex items-center justify-center" style={{ transform: `translateX(${slot * 100}%)` }}>
              <img
                src={image.url}
                alt={image.originalFileName}
                className="max-w-[min(56rem,100%)] max-h-[85vh] object-contain"
                onClick={(e) => e.stopPropagation()}
              />
            </div>
          );
        })}
      </div>
      <button className="absolute top-4 right-4 text-[#7a7568] hover:text-white transition-colors" onClick={onClose}>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
          <path d="M18 6L6 18M6 6L18 18" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
      </button>
      <button className="absolute left-4 top-1/2 -translate-y-1/2 text-[#7a7568] hover:text-white transition-colors p-2 hidden pointer-fine:block" onClick={(e) => { e.stopPropagation(); glide(-1); }}>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
          <path d="M15 18L9 12L15 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>
      <button className="absolute right-4 top-1/2 -translate-y-1/2 text-[#7a7568] hover:text-white transition-colors p-2 hidden pointer-fine:block" onClick={(e) => { e.stopPropagation(); glide(1); }}>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
          <path d="M9 18L15 12L9 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>
      <div className="absolute bottom-4 left-1/2 -translate-x-1/2 font-mono-data text-[11px] text-[#7a7568]">
        {index + 1} / {count}
      </div>
    </div>
  );
}
