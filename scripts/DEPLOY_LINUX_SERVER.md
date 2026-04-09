# Linux Server Deploy

## Package a build

```bash
./scripts/package_linux_server.sh "/Users/marlonboecker/Documents/MarpedBuilds/LinuxServer" \
  "/Users/marlonboecker/Documents/MarpedBuilds/LinuxServer.tar.gz"
```

## Deploy to DigitalOcean

```bash
./scripts/deploy_linux_server.sh \
  --host 161.35.177.72 \
  --user root \
  --key ~/.ssh/id_ed25519 \
  --archive "/Users/marlonboecker/Documents/MarpedBuilds/LinuxServer.tar.gz" \
  --scene-set BossFight
```

## Scene sets

Scene sets are defined in:

- `Assets/Scripts/Multiplayer/MultiplayerSceneSetCatalog.cs`

When you add a new multiplayer scene:

1. Add the scene to Unity's build scene list.
2. Add or update a scene set in `MultiplayerSceneSetCatalog`.
3. Deploy using `--scene-set YourSceneSetName`.
