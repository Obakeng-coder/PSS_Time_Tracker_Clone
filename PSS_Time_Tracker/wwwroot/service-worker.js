// Offline-first support for the Leave Request page (roadmap §8).
// Scope: caches the Leave/Create page shell so it still loads with no connection, and flushes
// queued leave requests (stored in IndexedDB by leave-offline.js) once connectivity returns.
//
// Background Sync ('sync' event below) works in Chromium browsers but has NO Safari/iOS support -
// leave-offline.js also listens for the page's own 'online' event as a fallback for those browsers,
// which calls the same flush logic. Both paths converge on flushQueue() here and there respectively.

const CACHE_NAME = "tymsheet-leave-v1";
const DB_NAME = "TymSheetOfflineDB";
const STORE_NAME = "leaveQueue";

const PRECACHE_URLS = [
    "/Leave/Create",
    "/manifest.json",
    "/assets/css/main.min.css",
    "/assets/css/custom.css",
    "/assets/plugins/bootstrap/css/bootstrap.min.css"
];

self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) =>
            // Best-effort: don't fail install if one asset 404s (e.g. a path that moves later).
            Promise.allSettled(PRECACHE_URLS.map((url) => cache.add(url)))
        )
    );
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener("fetch", (event) => {
    // Never intercept anything but plain GETs - POSTs (form submits, the offline queue flush
    // itself) must always hit the network directly, never be cached or short-circuited.
    if (event.request.method !== "GET") {
        return;
    }

    const url = new URL(event.request.url);
    if (url.origin !== self.location.origin) {
        return;
    }

    // Network-first for the Leave page itself, so a signed-in user always sees fresh data when
    // online; falls back to the cached shell only when the network is unreachable.
    if (url.pathname === "/Leave/Create") {
        event.respondWith(
            fetch(event.request)
                .then((response) => {
                    const copy = response.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
                    return response;
                })
                .catch(() => caches.match(event.request))
        );
        return;
    }

    // Cache-first for static assets.
    if (PRECACHE_URLS.includes(url.pathname)) {
        event.respondWith(
            caches.match(event.request).then((cached) => cached || fetch(event.request))
        );
    }
});

self.addEventListener("sync", (event) => {
    if (event.tag === "sync-leave-requests") {
        event.waitUntil(flushQueue());
    }
});

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, 1);
        req.onupgradeneeded = () => {
            if (!req.result.objectStoreNames.contains(STORE_NAME)) {
                req.result.createObjectStore(STORE_NAME, { keyPath: "id", autoIncrement: true });
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

async function flushQueue() {
    const db = await openDb();
    const items = await new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, "readonly");
        const req = tx.objectStore(STORE_NAME).getAll();
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });

    for (const item of items) {
        try {
            const body = new URLSearchParams(item.fields);
            const response = await fetch("/Leave/Create", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body,
                redirect: "follow"
            });

            // A successful Create redirects (302 -> Index). A validation failure re-renders the
            // Create form instead, which is ALSO a 200 - so response.ok alone can't distinguish
            // them; response.redirected is the only reliable signal that the POST actually saved.
            // Leave the item queued on a validation failure and let the user handle it manually
            // next time they open the app online, rather than silently losing the request.
            if (response.redirected) {
                await new Promise((resolve, reject) => {
                    const tx = db.transaction(STORE_NAME, "readwrite");
                    tx.objectStore(STORE_NAME).delete(item.id);
                    tx.oncomplete = resolve;
                    tx.onerror = reject;
                });
                await notifyClients({ type: "leave-request-synced", id: item.id });
            }
        } catch (err) {
            // Still offline or request failed - leave it queued, try again on the next sync/online event.
        }
    }
}

async function notifyClients(message) {
    const clients = await self.clients.matchAll();
    clients.forEach((client) => client.postMessage(message));
}

// Lets the page (leave-offline.js) trigger the same flush immediately on its 'online' event,
// for browsers (Safari/iOS) that don't support the Background Sync API at all.
self.addEventListener("message", (event) => {
    if (event.data === "flush-leave-queue") {
        event.waitUntil?.(flushQueue()) ?? flushQueue();
    }
});
