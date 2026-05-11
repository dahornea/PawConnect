window.pawConnect = window.pawConnect || {};

window.pawConnect.submitForm = (formId) => {
    const form = document.getElementById(formId);

    if (!form) {
        return;
    }

    if (typeof form.requestSubmit === "function") {
        form.requestSubmit();
        return;
    }

    form.submit();
};

window.pawConnect.maps = window.pawConnect.maps || {};

window.pawConnect.renderShelterMap = (elementId, latitude, longitude, shelterName, addressText) => {
    const element = document.getElementById(elementId);

    if (!element || typeof L === "undefined" || latitude === null || longitude === null) {
        return;
    }

    if (window.pawConnect.maps[elementId]) {
        const existing = window.pawConnect.maps[elementId];
        existing.map.invalidateSize();
        scheduleMapResize(existing.map);
        return;
    }

    waitForMapElementSize(element, () => initializeShelterMap(element, elementId, latitude, longitude, shelterName, addressText));
};

function initializeShelterMap(element, elementId, latitude, longitude, shelterName, addressText) {
    if (window.pawConnect.maps[elementId]) {
        scheduleMapResize(window.pawConnect.maps[elementId].map);
        return;
    }

    element.innerHTML = "";
    element.style.width = "100%";
    element.style.height = "100%";
    element.style.minHeight = "340px";

    const map = L.map(element, {
        scrollWheelZoom: false,
        dragging: true,
        zoomControl: true,
        attributionControl: true
    }).setView([latitude, longitude], 14);

    const tileLayer = L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    const popup = `<strong>${escapeHtml(shelterName || "Shelter")}</strong>${addressText ? `<br>${escapeHtml(addressText)}` : ""}`;
    L.marker([latitude, longitude], {
        icon: createShelterMarkerIcon(),
        title: shelterName || "Shelter"
    }).addTo(map).bindPopup(popup);

    const resizeObserver = typeof ResizeObserver === "undefined"
        ? null
        : new ResizeObserver(() => scheduleMapResize(map));

    resizeObserver?.observe(element);
    tileLayer.on("load", () => scheduleMapResize(map));

    window.pawConnect.maps[elementId] = { map, resizeObserver };

    scheduleMapResize(map);
}

window.pawConnect.disposeShelterMap = (elementId) => {
    const entry = window.pawConnect.maps[elementId];

    if (!entry) {
        return;
    }

    entry.resizeObserver?.disconnect();
    entry.map.remove();
    delete window.pawConnect.maps[elementId];
};

function createShelterMarkerIcon() {
    return L.divIcon({
        className: "pawconnect-map-marker",
        html: `
            <svg viewBox="0 0 34 46" aria-hidden="true" focusable="false">
                <path d="M17 44C13.8 38.7 4 29.4 4 17.5C4 9.8 9.8 4 17 4s13 5.8 13 13.5C30 29.4 20.2 38.7 17 44Z" />
                <circle cx="17" cy="17.5" r="6.5" />
            </svg>`,
        iconSize: [34, 46],
        iconAnchor: [17, 42],
        popupAnchor: [0, -36]
    });
}

function scheduleMapResize(map) {
    if (!map) {
        return;
    }

    requestAnimationFrame(() => map.invalidateSize({ pan: false }));
    setTimeout(() => map.invalidateSize({ pan: false }), 150);
    setTimeout(() => map.invalidateSize({ pan: false }), 350);
    setTimeout(() => map.invalidateSize({ pan: false }), 700);
}

function waitForMapElementSize(element, callback, attempt = 0) {
    const hasSize = element.clientWidth > 0 && element.clientHeight > 0;
    if (hasSize || attempt >= 10) {
        callback();
        return;
    }

    setTimeout(() => waitForMapElementSize(element, callback, attempt + 1), 50);
}

function escapeHtml(value) {
    const div = document.createElement("div");
    div.textContent = value;
    return div.innerHTML;
}
