# MTRM — Motores em Realidade Mista

Pacote público de código-fonte do instrumento experimental MTRM,
desenvolvido na UNESP-FEG para estudos de aprendizagem e interação com
um modelo tridimensional de motor.

## Conteúdo

- `unity/`: projeto Unity completo para execução e inspeção do código;
- `analysis/`: código Python do pipeline de análise e testes unitários;
- `tools/`: utilitários de validação e migração;
- `docs/component_mapping.csv`: vocabulário dos componentes;
- `docs/knowledge_items.csv`: banco de itens do protocolo;
- arquivos de licença e citação.

O pacote não contém dados de participantes, sessões de desenvolvimento,
capturas de tela, logs, anotações internas, histórico Git ou configurações
locais de computador.

## Execução

1. Instale o Unity Hub.
2. Adicione a pasta `unity/` como projeto.
3. Abra com Unity `2022.3.42f1`.
4. Abra `Assets/MTRM/Scenes/DesktopScene.unity`.
5. Entre em Play Mode.

A cena `Main_AR.unity` integra a infraestrutura de realidade aumentada, mas
não faz parte da configuração desktop habilitada por padrão.

## Privacidade

As sessões produzidas pela aplicação são gravadas fora do projeto Unity.
Utilize somente códigos pseudonimizados e nunca inclua dados de participantes
em distribuições públicas.
