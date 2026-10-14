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

    return { storage, theme, downloadText };
})();

// Installable app: the service worker caches nothing (see service-worker.js).
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/service-worker.js').catch(() => { /* not installable here; the site still works */ });
}
