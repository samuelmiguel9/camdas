namespace BellucSketch.Domain.Enums;

/// <summary>
/// Tipo de alteração proposta por uma <see cref="Entities.EdicaoPendenteCamada"/>. Cobre exatamente
/// as ações de edição que a Web expõe (visibilidade, opacidade, bloqueio, ordem e exclusão) — a Web
/// não tem ferramenta de desenho, então alterar o traço/imagem da camada não entra no fluxo de
/// aprovação, só o Android (mestre) desenha.
/// </summary>
public enum TipoOperacaoEdicaoPendente
{
    AlternarVisibilidade,
    DefinirOpacidade,
    AlternarBloqueio,
    Reordenar,
    Excluir
}
