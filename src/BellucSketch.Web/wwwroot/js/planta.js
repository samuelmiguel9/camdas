window.camdasInterop = {
    getElementSize: function (id) {
        var el = document.getElementById(id);
        if (!el) return null;
        return { width: el.clientWidth, height: el.clientHeight };
    },

    // Arrastar-para-rolar (pan) com o mouse: um <div style="overflow:auto"> só rola nativamente via
    // barra de rolagem ou toque — sem isto, dar zoom e tentar arrastar com o mouse pra navegar pela
    // planta não fazia nada (bug reportado: "não consigo mexer via zoom/pan"). Idempotente (guarda
    // via dataset) porque é chamado de novo a cada render do componente Blazor.
    habilitarArrastarPan: function (id) {
        var el = document.getElementById(id);
        if (!el || el.dataset.panHabilitado) return;
        el.dataset.panHabilitado = "1";
        el.style.touchAction = "none";

        var arrastando = false, ultimoX = 0, ultimoY = 0;

        el.addEventListener('pointerdown', function (e) {
            if (e.button !== 0 && e.pointerType === 'mouse') return;
            // Sem isto, arrastar com o mouse inicia a seleção de texto/imagem nativa do navegador em
            // vez de só rolar — funcionava "às vezes" mas ficava impossível de usar assim que havia
            // conteúdo pra selecionar em volta do canvas (bug reportado: "não consigo arrastar após o
            // zoom", quando o canvas passa a ser maior que a área visível).
            e.preventDefault();
            arrastando = true;
            ultimoX = e.clientX;
            ultimoY = e.clientY;
            el.setPointerCapture(e.pointerId);
            el.style.cursor = 'grabbing';
        });

        el.addEventListener('pointermove', function (e) {
            if (!arrastando) return;
            e.preventDefault();
            el.scrollLeft -= (e.clientX - ultimoX);
            el.scrollTop -= (e.clientY - ultimoY);
            ultimoX = e.clientX;
            ultimoY = e.clientY;
        });

        function pararArraste() {
            arrastando = false;
            el.style.cursor = 'grab';
        }

        el.addEventListener('pointerup', pararArraste);
        el.addEventListener('pointerleave', pararArraste);
        el.addEventListener('pointercancel', pararArraste);
        el.style.cursor = 'grab';
    }
};
