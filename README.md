# MPleno GeoEventos

Aplicativo desktop (Windows) que mostra, num mapa do Brasil, os **eventos de VTCD** (variação
de tensão de curta duração) e de **desvio de frequência** detectados pelas PMUs. Ele lê o
histórico gerado pelo detector de eventos (pasta BDED, um CSV por PMU e por mês) e o
`config.json` com o cadastro das PMUs.

Desenvolvido no contexto do projeto MPleno (ONS/FEESC, LabPlan-UFSC).

## O que ele faz

- **Mapa por PMU**: cada PMU vira um marcador colorido conforme o modo escolhido:
  - **Quantidade**: número de eventos no período;
  - **Severidade**: valor extremo (pu ou Hz);
  - **Duração**: ms para tensão, s para frequência;
  - **Indefinidos**: eventos `UNDEFINED`.
- **Filtros**: período, região (N, NE, CO, SE, S), tipo de evento (subtensão/sobretensão,
  subfrequência/sobrefrequência) e "somente séries íntegras" (descarta eventos com bits de
  qualidade ligados).
- **Ranking e contagem regional** das PMUs mais afetadas.
- **Detalhamento dos Eventos**: agrupa detecções próximas no tempo em uma mesma ocorrência.
  Mostra as PMUs atingidas, a ordem de propagação (★ marca a primeira detecção) e permite
  exportar para CSV.
- **Atualização automática**: quando o detector grava um CSV novo na pasta BDED, o mapa
  recarrega sozinho.

## Requisitos

| Para | Precisa de |
|---|---|
| Rodar | Windows 10/11 e [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (o app oferece instalar se faltar) |
| Compilar | [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) ou mais novo |
| Ver o mapa de fundo | Internet (os blocos do mapa vêm do OpenStreetMap) |

## Início rápido com o caso exemplo

O repositório já traz um caso pronto em [`exemplos/caso-basico`](exemplos/caso-basico). São
12 PMUs da rede MedFasee e 75 eventos **sintéticos** em junho e julho de 2026.

```bash
git clone https://github.com/GiovaneFsouza/Mapa.git
cd Mapa
dotnet run --project src/Mapa.App
```

Com o programa aberto, em **Fonte de Dados**:

1. **Histórico** → `…` → selecione a pasta `exemplos/caso-basico/bded`.
2. **Configuração** → `…` → selecione `exemplos/caso-basico/config.json`.
3. Em **Período**, escolha de 01/06/2026 a 31/07/2026.

O que procurar no mapa está descrito em [`exemplos/caso-basico/README.md`](exemplos/caso-basico/README.md).

## Usando com dados reais

1. Copie `exemplos/caso-basico/config.json` para outro lugar e cadastre as suas PMUs em `pmus`
   (veja [docs/configuracao.md](docs/configuracao.md)).
2. Aponte o **Histórico** para a pasta BDED que o detector de eventos grava
   (formato em [docs/formato-dados.md](docs/formato-dados.md)).

> ⚠️ O `config.json` real tem usuário e senha do RabbitMQ/PostgreSQL. Ele está no
> `.gitignore` e **nunca** deve ser enviado ao repositório.

Os caminhos escolhidos e os limites ajustados na tela ficam salvos em
`%AppData%\Mapa\settings.json`.

## Compilar, testar e gerar executável

```bash
dotnet build Mapa.slnx
```

```bash
dotnet test Mapa.slnx
```

```bash
dotnet publish src/Mapa.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

O último comando gera `publish/MPleno_GeoEventos.exe`, que roda em máquinas sem o .NET instalado.

## Estrutura do repositório

```
src/
  Mapa.App/          interface WPF + mapa Leaflet (WebView2)
  Mapa.Core/         leitura, filtros, agregação, correlação e escalas de cor (sem UI)
  Mapa.Core.Tests/   testes xUnit do Core
exemplos/
  caso-basico/       config.json + pasta BDED de exemplo
tools/
  gerar_exemplo.py   regenera o caso exemplo
docs/                documentação detalhada
```

## Documentação

- [Formato dos dados (BDED)](docs/formato-dados.md): colunas do CSV, tipos de evento e bits de qualidade
- [Configuração](docs/configuracao.md): `config.json` das PMUs e parâmetros do app
- [Arquitetura](docs/arquitetura.md): como o código está organizado
