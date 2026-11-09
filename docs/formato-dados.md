# Formato dos dados (BDED)

O histórico de eventos é uma árvore de pastas com um CSV por PMU e por mês:

```
<pasta BDED>/
  <pmu_id>/
    <ano>/
      <mês>.csv        ex.: GPA_UFSC/2026/06.csv
```

O leitor ([`CsvBdedReader`](../src/Mapa.Core/Import/CsvBdedReader.cs)) percorre **todos** os
`.csv` da árvore. Então os nomes de pasta e de arquivo são só organização: o que vale é o
conteúdo de cada linha.

## Colunas

Separador vírgula, decimal com ponto, primeira linha é o cabeçalho (a ordem das colunas
pode variar).

| Coluna | Exemplo | Descrição |
|---|---|---|
| `pmu_id` | `GPA_UFSC` | Identificador da PMU. Precisa existir em `pmus[].id` do `config.json` para aparecer no mapa. |
| `source_id` | `2` | Canal: `1` = frequência; `2`, `3`, `4` = tensão nas fases A, B, C. |
| `event_type` | `AMT` | Tipo do evento (tabela abaixo). |
| `date` | `2026-06-10` | Data do início (`yyyy-MM-dd`). |
| `start_time` | `14:32:05.122` | Hora de início (`HH:mm:ss.fff`). |
| `end_time` | `14:32:05.208` | Hora de fim (`HH:mm:ss.fff`). |
| `extreme_value` | `0.62` | Valor extremo: pu para tensão, Hz para frequência. |
| `quality` | `00000000` | 8 bits de qualidade, do bit 7 ao bit 0 (tabela abaixo). |

Linhas com erro não interrompem a leitura. Elas são listadas como avisos e o resto do
arquivo é aproveitado.

## Tipos de evento

| `event_type` | Grandeza | Sentido | Significado |
|---|---|---|---|
| `IMT` | Tensão | Baixo | Interrupção momentânea |
| `AMT` | Tensão | Baixo | Afundamento momentâneo |
| `EMT` | Tensão | Alto | Elevação momentânea |
| `ITT` | Tensão | Baixo | Interrupção temporária |
| `ATT` | Tensão | Baixo | Afundamento temporário |
| `ETT` | Tensão | Alto | Elevação temporária |
| `Underfrequency` | Frequência | Baixo | Subfrequência |
| `Overfrequency` | Frequência | Alto | Sobrefrequência |
| `UNDEFINED` | depende do `source_id` | Indeterminado | Evento que o detector não conseguiu caracterizar |

`UNDEFINED` com `source_id = 1` conta como frequência. Com qualquer outro `source_id`, conta
como tensão. Um `event_type` fora da tabela gera erro na linha.

## Bits de qualidade

A string é escrita do bit 7 (esquerda) ao bit 0 (direita).

| Bit | Valor | Significado |
|---|---|---|
| 0 | `00000001` | Início incerto |
| 1 | `00000010` | Fim incerto |
| 2 | `00000100` | Frames inválidos |
| 3 | `00001000` | Sem frames recebidos |
| 4 | `00010000` | Timeout |
| 5–7 | | Reservados |

Com o filtro **Somente séries íntegras** ligado, o app só considera eventos com `quality`
igual a `00000000`. A exceção é o modo **Indefinidos**: ele sempre mostra os `UNDEFINED`, que
normalmente já têm bits de qualidade ligados. Nos outros modos, os `UNDEFINED` ficam de fora.
