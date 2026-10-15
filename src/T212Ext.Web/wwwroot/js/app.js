// Small browser helpers called from Blazor through JS interop. Keep this file framework-free.
window.t212ext = (() => {
    const themeKey = 't212ext.theme';
    const darkQuery = window.matchMedia('(prefers-color-scheme: dark)');

    const storage = {
        get(key) { try { return localStorage.getItem(key); } catch { return null; } },
        set(key, value) { try { localStorage.setItem(key, value); } catch { /* private mode: not saved */ } },
        remove(key) { try { localStorage.removeItem(key); } catch { /* nothing to do */ } },
    };

    function applyTheme(choice) {
        const dark = choice === 'dark' || (choice !== 'light' && darkQuery.matches);
        document.documentElement.setAttribute('data-bs-theme', dark ? 'dark' : 'light');
        return dark;
    }

    const theme = {
        get: () => storage.get(themeKey) || 'auto',
        apply: applyTheme,
        set(choice) {
            storage.set(themeKey, choice);
            return applyTheme(choice);
        },
        // Follows the system setting while the choice is "auto", and tells .NET.
        watch(dotNetRef) {
            darkQuery.addEventListener('change', () => {
                if (theme.get() === 'auto')
                    dotNetRef.invokeMethodAsync('OnSystemThemeChanged', applyTheme('auto'));
            });
        },
    };

    // Saves text as a file. The BOM lets Excel read UTF-8 accents in CSV files.
    function downloadText(fileName, text, mimeType) {
        const blob = new Blob(['﻿', text], { type: mimeType || 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    }

    // Web Push, for the notifications page. Subscriptions go to .NET as plain strings.
    const push = (() => {
        const supported = () => 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
        const standalone = () => window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;

        // The service worker's registration, without hanging when it never registered.
        async function registration() {
            return (await navigator.serviceWorker.getRegistration('/')) || navigator.serviceWorker.register('/service-worker.js');
        }

        function toInfo(subscription) {
            const json = subscription.toJSON();
            return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
        }

        function keyBytes(base64Url) {
            const base64 = (base64Url + '='.repeat((4 - base64Url.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
            return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        }

        function sameKey(subscription, publicKey) {
            const current = subscription.options && subscription.options.applicationServerKey;
            if (!current) return false;
            const a = new Uint8Array(current), b = keyBytes(publicKey);
            return a.length === b.length && a.every((v, i) => v === b[i]);
        }

        // "Android · Chrome", "iPhone · Safari (app)": how the device shows in the list.
        function label() {
            const ua = navigator.userAgent;
            const os = /iPhone/.test(ua) ? 'iPhone' : /iPad/.test(ua) ? 'iPad' : /Android/.test(ua) ? 'Android'
                : /Windows/.test(ua) ? 'Windows' : /Mac OS X/.test(ua) ? 'Mac' : /Linux/.test(ua) ? 'Linux' : '?';
            const browser = /Edg\//.test(ua) ? 'Edge' : /Firefox\//.test(ua) ? 'Firefox' : /Chrome\//.test(ua) ? 'Chrome' : /Safari\//.test(ua) ? 'Safari' : '?';
            return `${os} · ${browser}${standalone() ? ' (app)' : ''}`;
        }

        return {
            status() {
                return {
                    supported: supported(),
                    permission: supported() ? Notification.permission : 'unsupported',
                    ios: /iPhone|iPad|iPod/.test(navigator.userAgent),
                    standalone: standalone(),
                    label: label(),
                    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || null,
                };
            },
            // Null too when the service worker cannot register (some embedded browsers).
            async current() {
                if (!supported()) return null;
                try {
                    const subscription = await (await registration()).pushManager.getSubscription();
                    return subscription ? toInfo(subscription) : null;
                } catch {
                    return null;
                }
            },
            // A native click handler: browsers (Safari above all) ask for permission only within the
            // click itself, which a round trip to the server would lose. .NET hears back through dotNetRef.
            bindEnable(button, dotNetRef, publicKey) {
                button.onclick = async () => {
                    try {
                        if (await Notification.requestPermission() !== 'granted') {
                            await dotNetRef.invokeMethodAsync('OnSubscribeFailed', 'denied');
                            return;
                        }
                        const reg = await registration();
                        let subscription = await reg.pushManager.getSubscription();
                        // Made with other keys (the server's changed): it cannot be used, so renew it.
                        if (subscription && !sameKey(subscription, publicKey)) {
                            await subscription.unsubscribe();
                            subscription = null;
                        }
                        subscription = subscription || await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(publicKey) });
                        await dotNetRef.invokeMethodAsync('OnSubscribed', toInfo(subscription), label());
                    } catch (e) {
                        await dotNetRef.invokeMethodAsync('OnSubscribeFailed', String(e && e.message || e));
                    }
                };
            },
            async unsubscribe() {
                if (!supported()) return;
                try {
                    const subscription = await (await registration()).pushManager.getSubscription();
                    if (subscription) await subscription.unsubscribe();
                } catch {
                    // Nothing subscribed here: the server's record is removed anyway.
                }
            },
        };
    })();

    return { storage, theme, downloadText, push };
})();

// Installable app: the service worker caches nothing (see service-worker.js).
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/service-worker.js').catch(() => { /* not installable here; the site still works */ });
}
