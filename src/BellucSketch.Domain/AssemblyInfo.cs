using System.Runtime.CompilerServices;

// Permite que os testes unitários exercitem entidades filhas (Camada, Cota) diretamente, mesmo com
// construtores/métodos "internal" que protegem o encapsulamento do agregado Planta contra o
// resto da aplicação (Application/Infrastructure/Api/Mobile).
[assembly: InternalsVisibleTo("BellucSketch.Domain.Tests")]
