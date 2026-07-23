// OCR da ferramenta de cota (Web) — usa Tesseract.js (motor WASM rodando 100% no navegador,
// mesmo espírito "on-device" do Google ML Kit usado pelo Android, sem chamada de rede nem chave de
// API). Todos os arquivos (script, worker, núcleo wasm, dados de treinamento em português) ficam
// vendorizados em wwwroot/lib/tesseract — sem CDN, pra funcionar offline/intranet.
window.camdasOcr = {
    _workerPromise: null,

    // O worker é caro de criar (carrega ~7MB de dados de treinamento na primeira vez) — mantido uma
    // única vez pela sessão da página, igual ao _reconhecedor singleton do OcrTextoService (Android).
    _getWorker: function () {
        if (!this._workerPromise) {
            this._workerPromise = Tesseract.createWorker('por', 1, {
                workerPath: 'lib/tesseract/worker.min.js',
                corePath: 'lib/tesseract/',
                langPath: 'lib/tesseract/lang',
            });
        }
        return this._workerPromise;
    },

    reconhecerAsync: async function (base64Png) {
        const worker = await this._getWorker();
        const resultado = await worker.recognize('data:image/png;base64,' + base64Png);
        return resultado.data.text;
    }
};
