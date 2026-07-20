namespace Camdas.Mobile.Relatorios;

/// <summary>
/// Changelog do app, em ordem crescente de versão (1.0 é a primeira). Cada entrada é adicionada à
/// mão quando uma rodada de mudanças é concluída — não é gerada automaticamente a partir do
/// histórico de commits (o projeto não usa controle de versão).
/// </summary>
public static class HistoricoVersoes
{
    public static IReadOnlyList<AtualizacaoVersao> Todas { get; } =
    [
        new AtualizacaoVersao(
            "1.0",
            new DateTime(2026, 7, 1, 10, 0, 0),
            "Pivô do app: de fluxo formal de ratificação/revisão de engenharia para desenho livre em camadas, estilo Paint.",
            [
                "Removido o fluxo de solicitação/aprovação/rejeição de revisão e de versionamento de planta.",
                "Camadas passaram a ser criadas livremente pelo usuário, com cor só no traço (não na camada).",
                "Traço livre por camada armazenado como bitmap raster (PNG), desenhado por cima da imagem importada.",
            ],
            [
                "Botões de ativar/desativar e bloquear camada não respondiam ao toque (gesto de seleção da linha inteira roubava o toque dos botões filhos) — corrigido restringindo o gesto só ao nome da camada.",
            ]),

        new AtualizacaoVersao(
            "1.1",
            new DateTime(2026, 7, 3, 14, 0, 0),
            "CRUD de projetos e edição de camada isolada.",
            [
                "Aba Projetos ganhou criar / editar / excluir (exclusão em cascata de plantas/camadas do projeto).",
                "Criar uma camada abre uma tela de edição isolada, mostrando só aquela camada — evita misturar o traço novo com as demais enquanto ainda está sendo desenhada.",
                "Ao voltar para a planta principal, todas as camadas já criadas aparecem juntas, com opção de ocultar cada uma.",
            ],
            [
                "A planta principal não estava re-renderizando todas as camadas depois de uma edição — causa: BindableProperty não reage a mutação in-place de Dictionary/ObservableCollection. Corrigido com assinatura em INotifyCollectionChanged e um refresh explícito ao voltar pra tela.",
            ]),

        new AtualizacaoVersao(
            "1.2",
            new DateTime(2026, 7, 5, 9, 0, 0),
            "Metadados de planta, prioridade de camadas e assinatura.",
            [
                "Cor deixou de existir por camada — só a cor do traço é escolhida pelo usuário.",
                "Planta ganhou Nome, Descrição (opcional) e Nome do cliente (opcional) ao importar.",
                "Adicionado controle de prioridade das camadas (reordenação com renumeração em ordem crescente).",
                "Assinatura \"developed by Samuel Miguel\" centralizada no rodapé da tela de projetos.",
            ],
            []),

        new AtualizacaoVersao(
            "1.3",
            DateTime.Now,
            "Limpeza de código morto, correções de UI reportadas em teste no aparelho, e relatório em PDF.",
            [
                "Removida por completo a entidade Cota (e Ponto2D/Medida/UnidadeMedida) — recurso da versão antiga de \"ratificação\", sem nenhum uso na UI atual; removida em todas as camadas (Domain/Application/Infrastructure/Api/Contracts/Mobile) e migration recriada.",
                "Apagados os arquivos de teste que só cobriam Cota/ValueObjects; testes remanescentes ajustados para as novas assinaturas (CamadaDto sem lista de Cotas, PlantaOverlayRenderer só com raster).",
                "Botão de arrastar (drag-and-drop) trocado por botões ▲/▼ de subir/descer — o DragGestureRecognizer nativo não respondia de forma confiável no Android.",
                "Layout da tela principal da planta rebalanceado (canvas com peso 7x contra 2x da lista de camadas) para a planta parar de aparecer cortada na visualização geral.",
                "Espessura da borracha agora numa escala própria e maior (até 120, contra 24 do traço normal), evitando ter que passar várias vezes pra apagar uma área grande.",
                "Lista de plantas do projeto ganhou uma pré-visualização (miniatura) da planta já composta com todas as camadas, do lado oposto ao nome.",
                "Adicionado este relatório em PDF, acessível por um botão no canto superior da tela de Projetos, com o changelog versionado.",
            ],
            [
                "Botão de arrastar reordenar camadas não funcionava (relatado em teste no aparelho) — corrigido trocando por botões subir/descer.",
                "Planta cortada na visualização geral (relatado em teste no aparelho) — corrigido redistribuindo o espaço do layout entre canvas e lista de camadas.",
            ]),

        new AtualizacaoVersao(
            "2.0.0",
            DateTime.Now,
            "Estabilização do SkiaSharp 3.x (fim do crash nativo recorrente) e edição de camada movida pra dentro da tela principal.",
            [
                "SkiaSharp atualizado pra 3.119.4 (Camdas.Mobile em net9.0-android).",
                "PlantaOverlayRenderer.PodeDesenhar valida todo bitmap antes de qualquer DrawBitmap (Handle/dimensões/ReadyToDraw) — elimina o SIGSEGV nativo (sk_image_new_from_bitmap) que persistia mesmo após a atualização do SkiaSharp.",
                "Zoom da tela de visualização agora tem teto calculado pelo tamanho da imagem base, pra a superfície do canvas nunca estourar o limite de bitmap do Android.",
                "Criar/editar uma camada não abre mais uma tela separada: acontece na própria tela de visualização (PlantaPage), com as demais camadas visíveis por baixo, evitando medida redundante.",
                "Borracha virou um ícone dedicado na barra de ferramentas (antes era um Switch separado).",
                "Lixeira de excluir camada (arrastar e soltar) mais larga e só acende quando a camada arrastada está de fato sobre ela (listener nativo Android — o MAUI não expõe essa posição no Android).",
                "Texto adicionado ao desenho aparece \"solto\" (arrastável) até confirmar a posição, em vez de já cravar direto no traço.",
                "Menu de opções da camada (opacidade, bloqueios, duplicar, excluir) encolhido e movido pro canto, deixando a planta visível por trás pra ver o efeito da opacidade em tempo real.",
                "Espessura mínima do traço reduzida (permite um traço bem mais fino).",
            ],
            [
                "SIGSEGV nativo recorrente ao interagir com a tela (teclado abrindo/fechando, S Pen, trocar de camada) — causa raiz: SkiaSharp 3.x chama sk_image_new_from_bitmap internamente em todo DrawBitmap, e crasha se o bitmap já foi liberado ou está sem pixels; nenhum try/catch em C# intercepta esse crash nativo.",
                "\"Canvas: trying to draw too large bitmap\" ao dar zoom alto numa planta grande — corrigido limitando o zoom máximo pelo tamanho da imagem base.",
                "Arrastar um texto recém-adicionado não se movia (travava no ponto inicial) — mesma causa (e mesma correção) do bug antigo do traço: o ScrollView pai roubava o gesto sem RequestDisallowInterceptTouchEvent.",
            ]),
    ];
}
