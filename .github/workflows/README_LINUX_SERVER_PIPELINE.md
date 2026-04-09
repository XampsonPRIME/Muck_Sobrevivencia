# Linux Server Pipeline

## Stable branch

`main` is treated as the stable branch.

Every push to `main` will:

1. Build the Unity Linux dedicated server.
2. Package it as `LinuxServer.tar.gz`.
3. Deploy it to the DigitalOcean droplet.
4. Restart `muck-server` with `sceneSet=Overworld` and port `7777`.

## Manual runs

You can also run the workflow manually from the GitHub Actions tab and choose:

- `scene_set`
- `port`
- `deploy`

## Required GitHub secrets

Unity / GameCI:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`
- `UNITY_SERIAL` (only if your license needs it)

DigitalOcean:

- `DO_HOST`
- `DO_USER`
- `DO_SSH_PRIVATE_KEY`

## Important for new scenes

When you add a new multiplayer scene:

1. Add the scene to Unity's build scene list.
2. Update `Assets/Scripts/Multiplayer/MultiplayerSceneSetCatalog.cs`.
3. If it should be the stable production map, update the workflow default `SCENE_SET_VALUE`.
