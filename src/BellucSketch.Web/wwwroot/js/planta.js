window.camdasInterop = {
    // Chamado no pointerdown da ferramenta de desenho (PlantaCanvasEdicaoWeb) — sem isto, um arrasto
    // rápido que sai da área do elemento no meio do gesto perde os eventos pointermove seguintes
    // (o navegador só entrega pointermove pro elemento se o ponteiro ainda estiver sobre ele).
    capturarPonteiro: function (id, pointerId) {
        var el = document.getElementById(id);
        el?.setPointerCapture(pointerId);
    },

    getElementSize: function (id) {
        var el = document.getElementById(id);
        if (!el) return null;
        return { width: el.clientWidth, height: el.clientHeight };
    },

    // Liga/desliga o pan por arrasto do wrapper enquanto a ferramenta de desenho (PlantaCanvasEdicaoWeb)
    // está ativa — os dois competiriam pelo mesmo pointerdown/pointermove (desenhar E rolar ao mesmo
    // tempo). Chamado toda vez que ViewModel.ModoEdicao muda (ver Planta.razor).
    definirModoDesenho: function (id, ativo) {
        var el = document.getElementById(id);
        if (el) el.dataset.desenhoAtivo = ativo ? "1" : "0";
    },

    // Baixa uma imagem (base64 PNG) como arquivo — usado pelo botão "Baixar" da barra de ações da
    // planta (Web). Sem API de download nativa em Blazor WASM pra isto: cria um <a download> temporário
    // com a data URL e simula o clique.
    baixarArquivo: function (nomeArquivo, base64Png) {
        var link = document.createElement('a');
        link.href = 'data:image/png;base64,' + base64Png;
        link.download = nomeArquivo;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },

    // Zoom com Ctrl+scroll (equivalente desktop da pinça de zoom do tablet — pedido do usuário: no
    // mouse não tem pinça). Sem "passive: false" o preventDefault não funciona e o navegador aplica o
    // próprio zoom de página junto (Ctrl+scroll nativo do Chrome/Firefox), competindo com o slider.
    // Idempotente (guarda via dataset) pelo mesmo motivo do habilitarArrastarPan.
    habilitarZoomCtrlScroll: function (id, dotNetRef) {
        var el = document.getElementById(id);
        if (!el || el.dataset.zoomCtrlHabilitado) return;
        el.dataset.zoomCtrlHabilitado = "1";

        el.addEventListener('wheel', function (e) {
            if (!e.ctrlKey) return;
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnCtrlScrollZoom', e.deltaY);
        }, { passive: false });
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
            if (el.dataset.desenhoAtivo === "1") return;
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
