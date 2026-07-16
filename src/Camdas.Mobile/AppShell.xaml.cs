using Camdas.Mobile.Views;

namespace Camdas.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(ProjetosPage), typeof(ProjetosPage));
        Routing.RegisterRoute(nameof(PlantasDoProjetoPage), typeof(PlantasDoProjetoPage));
        Routing.RegisterRoute(nameof(PlantaPage), typeof(PlantaPage));
        Routing.RegisterRoute(nameof(CamadaEdicaoPage), typeof(CamadaEdicaoPage));
        Routing.RegisterRoute(nameof(HistoricoPage), typeof(HistoricoPage));
        Routing.RegisterRoute(nameof(RevisaoEdicoesPage), typeof(RevisaoEdicoesPage));
    }
}
