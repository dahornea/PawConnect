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

window.pawConnect.downloadFileFromBase64 = (fileName, contentType, base64Data) => {
    const binary = atob(base64Data);
    const bytes = new Uint8Array(binary.length);

    for (let index = 0; index < binary.length; index += 1) {
        bytes[index] = binary.charCodeAt(index);
    }

    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "pawconnect-export";
    link.style.display = "none";

    document.body.appendChild(link);
    link.click();
    link.remove();

    setTimeout(() => URL.revokeObjectURL(url), 1000);
};

window.pawConnect.getCurrentLocation = () => new Promise(resolve => {
    if (!navigator.geolocation) {
        resolve({
            success: false,
            errorCode: "unsupported",
            message: "Geolocation is not supported by this browser."
        });
        return;
    }

    navigator.geolocation.getCurrentPosition(
        position => {
            resolve({
                success: true,
                latitude: position.coords.latitude,
                longitude: position.coords.longitude
            });
        },
        error => {
            const errorCode = error.code === error.PERMISSION_DENIED
                ? "permission-denied"
                : error.code === error.POSITION_UNAVAILABLE
                    ? "position-unavailable"
                    : error.code === error.TIMEOUT
                        ? "timeout"
                        : "unknown";

            resolve({
                success: false,
                errorCode,
                message: error.message || "Browser location could not be retrieved."
            });
        },
        {
            enableHighAccuracy: false,
            timeout: 10000,
            maximumAge: 300000
        });
});

window.pawConnect.maps = window.pawConnect.maps || {};

window.pawConnect.renderShelterMap = (elementId, latitude, longitude, shelterName, addressText, editable = false, dotNetReference = null) => {
    const element = document.getElementById(elementId);

    if (!element || typeof L === "undefined") {
        return;
    }

    if (window.pawConnect.maps[elementId]) {
        const existing = window.pawConnect.maps[elementId];
        existing.dotNetReference = dotNetReference || existing.dotNetReference;
        existing.editable = editable;
        updateShelterMapMarker(existing, latitude, longitude, shelterName, addressText, editable);
        scheduleMapResize(existing.map);
        return;
    }

    waitForMapElementSize(element, () => initializeShelterMap(element, elementId, latitude, longitude, shelterName, addressText, editable, dotNetReference));
};

function initializeShelterMap(element, elementId, latitude, longitude, shelterName, addressText, editable, dotNetReference) {
    if (window.pawConnect.maps[elementId]) {
        const existing = window.pawConnect.maps[elementId];
        updateShelterMapMarker(existing, latitude, longitude, shelterName, addressText, editable);
        scheduleMapResize(existing.map);
        return;
    }

    element.innerHTML = "";
    element.style.width = "100%";
    element.style.height = "100%";
    element.style.minHeight = "340px";

    const start = getMapStart(latitude, longitude);
    const map = L.map(element, {
        scrollWheelZoom: false,
        dragging: true,
        zoomControl: true,
        attributionControl: true
    }).setView([start.latitude, start.longitude], start.hasMarker ? 14 : 12);

    const tileLayer = L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);

    const entry = {
        map,
        marker: null,
        resizeObserver: null,
        editable,
        dotNetReference
    };

    if (start.hasMarker) {
        setShelterMarker(entry, latitude, longitude, shelterName, addressText, editable, false);
    }

    if (editable) {
        map.on("click", event => {
            setShelterMarker(entry, event.latlng.lat, event.latlng.lng, shelterName, addressText, true, true);
        });
    }

    const resizeObserver = typeof ResizeObserver === "undefined"
        ? null
        : new ResizeObserver(() => scheduleMapResize(map));

    resizeObserver?.observe(element);
    tileLayer.on("load", () => scheduleMapResize(map));
    entry.resizeObserver = resizeObserver;

    window.pawConnect.maps[elementId] = entry;

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

function updateShelterMapMarker(entry, latitude, longitude, shelterName, addressText, editable) {
    if (!entry) {
        return;
    }

    entry.editable = editable;
    const start = getMapStart(latitude, longitude);
    if (!start.hasMarker) {
        if (entry.marker) {
            entry.map.removeLayer(entry.marker);
            entry.marker = null;
        }
        scheduleMapResize(entry.map);
        return;
    }

    setShelterMarker(entry, latitude, longitude, shelterName, addressText, editable, false);
    entry.map.setView([latitude, longitude], Math.max(entry.map.getZoom(), 14), { animate: true });
    scheduleMapResize(entry.map);
}

function setShelterMarker(entry, latitude, longitude, shelterName, addressText, editable, notifyBlazor) {
    const popup = `<strong>${escapeHtml(shelterName || "Shelter")}</strong>${addressText ? `<br>${escapeHtml(addressText)}` : ""}`;

    if (!entry.marker) {
        entry.marker = L.marker([latitude, longitude], {
            icon: createShelterMarkerIcon(),
            title: shelterName || "Shelter",
            draggable: editable
        }).addTo(entry.map).bindPopup(popup);

        entry.marker.on("dragend", event => {
            const position = event.target.getLatLng();
            notifyCoordinateChanged(entry, position.lat, position.lng);
        });
    } else {
        entry.marker.setLatLng([latitude, longitude]);
        entry.marker.setPopupContent(popup);
        if (editable) {
            entry.marker.dragging?.enable();
        } else {
            entry.marker.dragging?.disable();
        }
    }

    if (notifyBlazor) {
        notifyCoordinateChanged(entry, latitude, longitude);
    }
}

function notifyCoordinateChanged(entry, latitude, longitude) {
    if (!entry?.dotNetReference) {
        return;
    }

    entry.dotNetReference.invokeMethodAsync("OnMapCoordinatesChanged", roundCoordinate(latitude), roundCoordinate(longitude));
}

function getMapStart(latitude, longitude) {
    const hasMarker = latitude !== null &&
        longitude !== null &&
        latitude !== undefined &&
        longitude !== undefined &&
        !Number.isNaN(Number(latitude)) &&
        !Number.isNaN(Number(longitude));

    return {
        latitude: hasMarker ? Number(latitude) : 46.7712,
        longitude: hasMarker ? Number(longitude) : 23.6236,
        hasMarker
    };
}

function roundCoordinate(value) {
    return Math.round(Number(value) * 1000000) / 1000000;
}

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
