# add-dandan-api-proxy-mode artifacts

- `Emby.Plugin.Danmu.dll`
  - Size: 1,197,056 bytes
  - SHA-256: `880537df21380e641c8840e7ed7e5f3446235cf72acfa48f7d2880624ed8ce89`
- `emby-plugin-danmuplus-add-dandan-api-proxy-mode-source.zip`
  - Size: 692,027 bytes
  - SHA-256: `c642a36ccaeb4eb89056e7b7466e30aa06d2972a297c9b546e30781ba062bb57`

The DLL is the Release artifact deployed for the live proxy verification. The
source archive excludes Git metadata and generated `bin`, `obj`, `dist`,
`releases`, and `artifacts` directories. It contains no Emby configuration,
server log, backup file, API credential, account credential, or access token.

Verification details and exact rollback paths are recorded in
`openspec/changes/add-dandan-api-proxy-mode/verification.md`.
