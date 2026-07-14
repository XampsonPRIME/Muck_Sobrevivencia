# Publicacao da demo no itch.io

## Gerar a build

No Unity, use:

`Tools > Build > Build Windows Release (itch.io)`

Essa build usa o backend Mono para nao depender do compilador C++ do Visual Studio.

O processo gera:

- `Builds/Windows/Elarion-Relics-of-the-Forgotten-Windows-x64/`
- `Builds/Windows/Elarion-Relics-of-the-Forgotten-Windows-x64-0.1.0.zip`

O ZIP contem o executavel, a pasta de dados, bibliotecas do Unity, versao e instrucoes para o jogador.
Nao envie somente o arquivo `.exe`.

## Backend de scripting

Para a demo Windows no itch.io, mantenha:

- `Edit > Project Settings > Player > Windows > Other Settings > Scripting Backend: Mono`

O IL2CPP continua sendo uma opcao para uma release futura, mas exige:

- Visual Studio 2022 com o workload `Desenvolvimento para desktop com C++`.
- MSVC v143 para x64/x86.
- Windows 10 ou 11 SDK, versao 10.0.19041.0 ou superior.

Sem esses componentes, o Unity encerra a build com `ToolchainNotFoundException`.

## Teste antes de publicar

1. Extraia o ZIP em uma pasta fora do projeto.
2. Abra o executavel e inicie uma nova aventura.
3. Verifique carregamento do mundo, save/load, audio, tela cheia e menu de pausa.
4. Teste um totem comum e confirme o XP e o buff.
5. Teste host e cliente LAN em dois executaveis, se possivel em computadores diferentes.
6. Derrote Kael'Tor e valide as opcoes de continuar e sair.

## Criar a pagina

Configuracao inicial sugerida:

- Title: `Elarion: Relics of the Forgotten`
- Kind of project: `Downloadable`
- Release status: `In development`
- Pricing: gratuito ou `No payments`, enquanto for demo
- Platform: `Windows`
- Architecture: `64-bit`
- Version: `0.1.0-demo`
- Visibility inicial: `Restricted`, para um ultimo teste privado

Inclua uma capa legivel, de tres a cinco screenshots reais do gameplay, descricao curta, controles,
recursos atuais da demo e uma indicacao clara de que o multiplayer e LAN.

Documentacao oficial:

- https://itch.io/docs/creators/getting-started
- https://itch.io/docs/creators/quality-guidelines
- https://itch.io/docs/butler/

## Publicar com Butler

Depois de instalar e autenticar o Butler:

```powershell
.\scripts\publish_itch_windows.ps1 `
  -ItchUser "SEU_USUARIO" `
  -GameSlug "SLUG_DA_PAGINA" `
  -Version "0.1.0-demo"
```

O canal padrao sera `windows-demo`. Nas atualizacoes seguintes, execute o mesmo comando com a nova versao.
O Butler envia apenas as diferencas entre as builds.

## Texto curto sugerido

> Explore um mundo procedural de fantasia survival, escolha um poder unico, cace criaturas, fabrique
> equipamentos, desafie Totens Ancestrais e descubra o segredo protegido por Kael'Tor.

Marque claramente na pagina:

- Demo em desenvolvimento.
- Windows 64-bit.
- Teclado e mouse.
- Multiplayer LAN experimental.
- Save local.
