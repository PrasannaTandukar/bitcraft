/**
 * Route configuration for Tutor Studio.
 *
 * Each entry defines a client-side route. The `guard` field selects the
 * route wrapper: "protected" (authenticated), "admin" (Admin role), or
 * "role" with an `allowedRoles` array. Pages without a guard are public.
 *
 * Navigation grouping is expressed via `group` (the dropdown label) and
 * `order` (position within the group). Standalone links have no group.
 */

export const routes = [
    // Standalone links
    { path: "/", label: "Dashboard", element: null, guard: "protected", order: 0 },
    { path: "/chat", label: "Chat", element: null, guard: "protected", order: 1 },

    // Documents group
    { path: "/documents", label: "Browse", element: null, guard: "role", allowedRoles: ["Auditor", "Editor", "Admin"], group: "Documents", order: 0 },
    { path: "/documents/upload", label: "Upload", element: null, guard: "role", allowedRoles: ["Editor", "Admin"], group: "Documents", order: 1 },

    // Tutors group
    { path: "/tutors", label: "Tutors", element: null, guard: "role", allowedRoles: ["Auditor", "Editor", "Admin"], group: "Tutors", order: 0 },
    { path: "/tutors/:tutorId", label: null, element: null, guard: "role", allowedRoles: ["Auditor", "Editor", "Admin"], group: "Tutors", order: 1, hidden: true },
    { path: "/benchmarking", label: "Benchmarking", element: null, guard: "role", allowedRoles: ["Auditor", "Editor", "Admin"], group: "Tutors", order: 2 },
    { path: "/tutor-setup", label: "Tutor Setup", element: null, guard: "role", allowedRoles: ["Editor", "Admin"], group: "Tutors", order: 3 },

    // Admin group
    { path: "/admin/roles", label: "Role Management", element: null, guard: "admin", group: "Admin", order: 0 },
    { path: "/admin/embedding-models", label: "Embedding Models", element: null, guard: "admin", group: "Admin", order: 1 },
    { path: "/admin/ai-models", label: "AI Models", element: null, guard: "admin", group: "Admin", order: 2 },
    { path: "/admin/ai-providers", label: "AI Providers", element: null, guard: "admin", group: "Admin", order: 3 },

    // User group
    { path: "/profile", label: "Profile", element: null, guard: "protected", group: "User", order: 0 },
    { path: "/settings", label: "Settings", element: null, guard: "protected", group: "User", order: 1 },

    // Redirects (legacy routes)
    { path: "/benchmark-templates", label: null, element: null, redirect: "/benchmarking?tab=templates", hidden: true },
];

/**
 * Grouped navigation items derived from the flat route list.
 * Returns { standalone: Route[], groups: { label: string, items: Route[] }[] }.
 */
export function getNavigationGroups(routesList) {
    const standalone = [];
    const groupMap = new Map();

    for (const route of routesList) {
        if (route.hidden) continue;
        if (route.group) {
            if (!groupMap.has(route.group)) {
                groupMap.set(route.group, { label: route.group, items: [] });
            }
            groupMap.get(route.group).items.push(route);
        } else {
            standalone.push(route);
        }
    }

    // Sort groups by the minimum order of their items
    const groups = Array.from(groupMap.values()).sort((a, b) => {
        const aMin = Math.min(...a.items.map((item) => item.order ?? 0));
        const bMin = Math.min(...b.items.map((item) => item.order ?? 0));
        return aMin - bMin;
    });

    // Sort items within each group
    for (const group of groups) {
        group.items.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
    }

    standalone.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

    return { standalone, groups };
}

/**
 * Check if a route is visible to the current user.
 */
export function isRouteVisible(route, roles) {
    if (!roles || roles.length === 0) return false;
    if (route.guard === "admin") return roles.includes("Admin");
    if (route.guard === "role" && route.allowedRoles) {
        return route.allowedRoles.some((role) => roles.includes(role));
    }
    return true;
}
