namespace Camdas.Mobile.Services;

/// <summary>
/// Guarda a lista de endereços de servidor já usados pelo app (persistida no dispositivo), para que
/// o usuário não precise redigitar o IP toda vez que trocar de rede (ex.: casa/trabalho).
/// </summary>
public interface IArmazenamentoEnderecosApi
{
    /// <summary>
    /// Todos os endereços salvos, com o endereço ativo (último usado com sucesso) primeiro na lista.
    /// Na primeira execução, semeia a lista com um endereço padrão.
    /// </summary>
    IReadOnlyList<EnderecoApi> Listar();

    /// <summary>Adiciona (ou atualiza, se o nome já existir) um endereço e o marca como ativo.</summary>
    void Salvar(EnderecoApi endereco);

    /// <summary>O último endereço marcado como ativo, se houver.</summary>
    EnderecoApi? ObterAtivo();
}
