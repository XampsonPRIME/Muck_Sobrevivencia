# Tela inicial — direção visual e validação

A apresentação é aplicada por LobbyPresentation no Start de LobbyUI, tanto ao prefab existente quanto à UI criada em runtime. O prefab original continua visível fora do Play Mode. As fontes globais não foram alteradas.

Arte: Assets/Resources/MainMenu/Sanctuary.png. Gerada com a ferramenta integrada image_gen.

Prompt final: Use case: stylized-concept. Create a premium cinematic dark fantasy game main-menu background, landscape 16:9, 2560x1440. No text, no logos, no interface. Ancient ruined stone sanctuary in a misty forest at night, extremely detailed weathered carved stone, tangled roots, distant mountain silhouettes and layered blue-green volumetric fog. On the RIGHT third a monumental broken circular stone arch surrounds a small floating amber-gold relic above an ancient pedestal, warm light spilling on wet stone steps. Restrained gold embers, dramatic atmospheric depth, sophisticated desaturated midnight teal / charcoal palette and luminous antique gold focal point. LEFT 45 percent deliberately dark quiet negative space for readable title and menu overlay, still subtle atmospheric detail. Elegant high-end fantasy art direction, realistic materials, cinematic lighting, sharp crafted environment details, no people, no oversaturated purple, no UI.

A imagem entregue pelo gerador tem 1672 × 941 pixels. O fundo usa preenchimento proporcional; os elementos interativos usam área segura de 1600 × 900. Movimento e entrada usam tempo não escalado, com preferência persistente para desativar a animação.

Regressão corrigida: os textos dos botões do prefab tinham âncoras centrais; aplicar margens sem convertê-las para stretch produzia largura negativa e letras empilhadas. Agora o layout define as âncoras antes das margens, desativa quebra de linha nos botões e isola a escala do menu da escala de HUD.

Validação: compilação com o compilador e o response file do Unity 6000.3.6f1. Apenas warning preexistente CS0414 em InventorySlotUI. Conferência visual em Play Mode do menu e janela multiplayer. Sessões em rede não foram exercitadas com outro dispositivo.

## Biblioteca de mundos solo

O menu agora mantém as ações Nova jornada e Continuar jornada sempre disponíveis. Nova jornada exige um nome válido (1–32 caracteres, sem duplicatas); Continuar jornada abre três espaços com nomes, dia e horário do último save, entrada no mundo, renomeação e exclusão confirmada. Um mundo sem primeiro autosave mantém sua semente e pode ser iniciado novamente.

Persistência: `Application.persistentDataPath/solo_worlds/<GUID>.json`. O GUID não muda ao renomear. Nomes são texto simples e nunca viram caminhos. Cada arquivo contém metadados e o SaveGameData existente. Há no máximo três mundos, contando arquivos ilegíveis. Escritas usam arquivo temporário e substituição atômica com backup; um arquivo danificado tenta recuperar a versão anterior. O autosave grava somente o mundo selecionado e aguarda o término do carregamento.

Compatibilidade: `savegame.json` é importado uma vez como “Meu primeiro mundo”; o original permanece intacto e um marcador impede que ele reapareça depois de uma exclusão deliberada. Os saves multiplayer mantêm seu formato e seus caminhos.

O rodapé ganhou uma superfície escura, texto maior e botão de animação com destaque de foco e estado explícito. Todos os painéis preservam a paleta de verde profundo, marfim e dourado.

Testes de persistência: `Assets/Editor/SoloWorldStoreTests.cs`, executáveis no Unity por Tools > Elarion > Validate Solo World Saves. Os testes usam somente fixtures em Temp/SoloWorldTests. O relatório é gerado em Temp/SoloWorldTests/results.txt.
