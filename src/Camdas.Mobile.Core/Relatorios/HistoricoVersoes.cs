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
    ];
}
