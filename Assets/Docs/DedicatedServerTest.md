# Dedicated Server Test

Use a mesma build do jogo para cliente e servidor.

## Scene sets disponiveis

- `Overworld`: `Main + PlayerTest`
- `BossFight`: `Main + PlayerTest + Boss1`

## Build

Garanta que estas scenes estao no `Build Settings`:

- `Main`
- `PlayerTest`
- `Boss1`
- `Inventario`

## Subir servidor de teste

Windows:

```bat
SeuJogo.exe -dedicatedServer -sceneSet Overworld -port 7777
```

macOS:

```bash
./SeuJogo.app/Contents/MacOS/SeuJogo -dedicatedServer -sceneSet Overworld -port 7777
```

Opcional:

- `-sceneSet BossFight`
- `-worldSeed 12345`

## Conectar cliente

Abra o jogo normalmente e entre com:

- IP do servidor
- porta `7777`

## Observacoes

- O servidor sobe sem HUD, lobby e pause.
- O cliente recebe o pacote de scenes ativo do servidor.
- Se o host/servidor trocar o pacote de scenes, os clientes acompanham.
