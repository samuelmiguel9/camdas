window.camdasInterop = {
    getElementSize: function (id) {
        var el = document.getElementById(id);
        if (!el) return null;
        return { width: el.clientWidth, height: el.clientHeight };
    }
};
