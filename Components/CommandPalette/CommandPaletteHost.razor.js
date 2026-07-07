let commandPaletteHandler;

export function registerCommandPaletteShortcuts(dotNetReference) {
    unregisterCommandPaletteShortcuts();

    commandPaletteHandler = (event) => {
        const target = event.target;
        const tagName = target?.tagName?.toLowerCase();
        const isTextInput =
            tagName === "input" ||
            tagName === "textarea" ||
            tagName === "select" ||
            target?.isContentEditable;

        const isCommandShortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k";
        const isSlashShortcut = !isTextInput && event.key === "/";

        if (!isCommandShortcut && !isSlashShortcut) {
            return;
        }

        event.preventDefault();
        dotNetReference.invokeMethodAsync("OpenFromShortcut");
    };

    document.addEventListener("keydown", commandPaletteHandler);
}

export function unregisterCommandPaletteShortcuts() {
    if (!commandPaletteHandler) {
        return;
    }

    document.removeEventListener("keydown", commandPaletteHandler);
    commandPaletteHandler = undefined;
}

export function focusElement(element) {
    element?.focus();
}

export function getRecentCommands() {
    try {
        return JSON.parse(localStorage.getItem("pawconnect.commandPalette.recent") || "[]");
    } catch {
        return [];
    }
}

export function saveRecentCommand(command, limit) {
    const safeCommand = {
        id: command.id,
        title: command.title,
        description: command.description,
        category: command.category,
        route: command.route,
        icon: command.icon,
        keywords: [],
        badge: command.badge,
        requiresConfirmation: false,
        isSensitive: false
    };

    const existing = getRecentCommands()
        .filter(item => item.id !== safeCommand.id && item.route !== safeCommand.route);

    localStorage.setItem(
        "pawconnect.commandPalette.recent",
        JSON.stringify([safeCommand, ...existing].slice(0, limit || 8)));
}
