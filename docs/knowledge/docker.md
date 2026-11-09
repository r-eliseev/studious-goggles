# The Web host in Docker

The root `Dockerfile` builds the Vue client and publishes the ASP.NET Core Web host in separate build stages. The final image contains the published app, the .NET runtime and a health-check utility; the sources, Node.js and the .NET SDK stay in the build stages. The container runs as the unprivileged user `app` (`APP_UID` from the official .NET image).

## Running

You need Docker Engine with Compose v2 and a free local port 8080. At the repository root set a password and start Compose:

```sh
export IMAGEGEN_WEB_PASSWORD='a-long-unique-password'
docker compose up -d --build
```

The password is mandatory, is not built into the image and has no default value. Compose publishes the app only on `127.0.0.1:8080`; to give access from the local network, change the port binding deliberately and set up TLS and network protection before publishing externally. Health endpoint: `http://127.0.0.1:8080/api/health`.

To stop, run `docker compose down`. This command keeps the named volume `imagegen-data`. Do not use `docker compose down --volumes` if the data must be kept. To update the image, rebuild the service: `docker compose up -d --build`.

## Persistent data and backups

All data of the Web host is stored under `/data` in the `imagegen-data` volume: the SQLite database, images, encrypted provider keys and the ASP.NET Data Protection keys. So the same volume is needed to decrypt the saved keys and the valid auth cookies after the container is recreated. Deleting the volume without a backup irreversibly deletes this data.

For a consistent copy, stop the service, save the volume as an archive and then start it again:

```sh
docker compose down
docker run --rm \
  -v imagegen-data:/data:ro \
  -v "$PWD:/backup" \
  alpine:3.22 tar -czf /backup/imagegen-data.tar.gz -C /data .
docker compose up -d
```

Keep the archive as a secret: it includes the database, the images and the material for decrypting the keys. To restore, the volume must be empty or cleaned beforehand, and the archive verified:

```sh
docker run --rm \
  -v imagegen-data:/data \
  -v "$PWD:/backup:ro" \
  alpine:3.22 tar -xzf /backup/imagegen-data.tar.gz -C /data
```

Compose uses a named volume that Docker creates with permissions compatible with the owner of the `/data` folder in the image. For a bind mount, create the folder beforehand and give it the UID used by the image (`docker run --rm imagegen-imagegen id app` after the build); do not give the folder `777` permissions.

## Changing the password

Change `IMAGEGEN_WEB_PASSWORD` in the environment, stop and recreate the container (`docker compose down && docker compose up -d`). The new password applies at startup, the old one stops passing the login check. Existing auth cookies are cryptographically protected by the persistent Data Protection key ring and may stay valid until they expire; if needed, end their session by deleting the volume only if you are ready to lose all data. Do not delete the key ring separately from the database and the saved secrets.
