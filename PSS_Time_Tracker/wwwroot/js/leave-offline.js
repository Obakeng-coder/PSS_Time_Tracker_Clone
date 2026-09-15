// Offline-first submission for the "Request Time Off" form (roadmap §8).
// When online: does nothing - the form submits normally, exactly as before this feature existed.
// When offline: queues the submission in IndexedDB, shows a "queued" banner instead of an error,
// and syncs it to the server automatically once connectivity returns (Background Sync where
// supported, a plain 'online' event listener everywhere else - notably Safari/iOS).

(function () {
    const DB_NAME = "TymSheetOfflineDB";
    const STORE_NAME = "leaveQueue";
    const FORM_ID = "leaveCreateForm";
    const BANNER_ID = "offlineQueueBanner";

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

    async function queueSubmission(fields) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_NAME, "readwrite");
            tx.objectStore(STORE_NAME).add({ fields, queuedAt: new Date().toISOString() });
            tx.oncomplete = resolve;
            tx.onerror = reject;
        });
    }

    async function queuedCount() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE_NAME, "readonly");
            const req = tx.objectStore(STORE_NAME).count();
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }

    function showBanner(text, variant) {
        let banner = document.getElementById(BANNER_ID);
        if (!banner) {
            banner = document.createElement("div");
            banner.id = BANNER_ID;
            banner.style.marginBottom = "1rem";
            const form = document.getElementById(FORM_ID);
            form?.parentElement?.insertBefore(banner, form);
        }
        banner.className = `alert alert-${variant || "warning"}`;
        banner.textContent = text;
    }

    function requestSync() {
        if ("serviceWorker" in navigator && "SyncManager" in window) {
            navigator.serviceWorker.ready
                .then((reg) => reg.sync.register("sync-leave-requests"))
                .catch(() => flushViaMessage());
        } else {
            // Safari/iOS and any browser without Background Sync support.
            flushViaMessage();
        }
    }

    function flushViaMessage() {
        navigator.serviceWorker?.controller?.postMessage("flush-leave-queue");
    }

    async function refreshBannerFromQueue() {
        const count = await queuedCount().catch(() => 0);
        if (count > 0) {
            showBanner(
                `You're offline. ${count} leave request(s) queued and will submit automatically once you're back online.`,
                "warning"
            );
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        if ("serviceWorker" in navigator) {
            navigator.serviceWorker.register("/service-worker.js").catch(() => {
                // Offline support just won't be available this session - the form still works
                // normally online, so this is non-fatal.
            });

            navigator.serviceWorker.addEventListener("message", (event) => {
                if (event.data?.type === "leave-request-synced") {
                    refreshBannerFromQueue().then(async () => {
                        if ((await queuedCount().catch(() => 0)) === 0) {
                            showBanner("Your queued leave request has been submitted.", "success");
                        }
                    });
                }
            });
        }

        const form = document.getElementById(FORM_ID);
        if (!form) {
            return;
        }

        refreshBannerFromQueue();

        form.addEventListener("submit", (event) => {
            if (navigator.onLine) {
                return; // let the normal POST happen, unchanged from before this feature existed
            }

            event.preventDefault();

            const fields = {};
            new FormData(form).forEach((value, key) => {
                fields[key] = value;
            });

            queueSubmission(fields)
                .then(() => {
                    showBanner(
                        "You're offline. Your leave request has been saved on this device and will " +
                        "submit automatically once you're back online.",
                        "warning"
                    );
                    form.reset();
                    requestSync();
                })
                .catch(() => {
                    showBanner("Couldn't save your request offline. Please try again once you're back online.", "danger");
                });
        });

        window.addEventListener("online", () => {
            showBanner("Back online - syncing your queued leave request(s)...", "info");
            requestSync();
        });
    });
})();
