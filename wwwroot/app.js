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
