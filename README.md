# KimiSync

以 ASP.NET Core / .NET 10 與 Microsoft SQL Server 建置的同步備份 API。客戶端取得 Token 後，即可儲存及讀取自己的備份內容。

## 開發環境

- .NET 10 SDK
- SQL Server；可使用本專案的 Docker Compose 啟動開發用 SQL Server 2022
- Docker Compose（使用容器資料庫時）

## 本機啟動

以下指令使用 Bash 或 Zsh，請在專案根目錄執行。

### 1. 啟動資料庫

設定符合 SQL Server 密碼規則的密碼，再啟動容器：

```sh
export MSSQL_SA_PASSWORD='Replace-With-Your-Strong-Password!'
docker compose up -d
```

等待 SQL Server 完成啟動後，建立資料庫：

```sh
docker compose exec mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q 'CREATE DATABASE KimiSync;'
```

若已有 SQL Server，可直接建立 `KimiSync` 資料庫並使用自己的連線資訊。

### 2. 設定連線與建立資料表

```sh
export ConnectionStrings__KimiSync="Server=localhost,1433;Database=KimiSync;User ID=sa;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
dotnet run -- --init-db
```

`--init-db` 會在指定的資料庫建立 `dbo.SyncData`。可重複執行；不會建立資料庫，也不會變更既有資料表。亦可由 DBA 執行 [Database/schema.sql](Database/schema.sql)。

### 3. 產生金鑰並啟動 API

首次啟動時產生金鑰：

```sh
dotnet run -- --new-secret
```

已有 `secret.key` 時請保留原檔；此指令會拒絕覆寫。更換金鑰會使原有 Token 失效。

```sh
export WEBSITE_URL=https://example.com
dotnet run
```

預設開發位址為 `http://localhost:5036`。請求須帶有與 `WEBSITE_URL` 相同的 `Origin`，否則回傳 403，包括首頁及 Token 簽發端點。

## 設定

可透過環境變數或 ASP.NET Core 設定檔提供設定。應用程式不會自動載入 `.env`；Docker Compose 的 `.env` 僅供 Compose 使用。

| 環境變數 | 預設值 | 用途 |
| --- | --- | --- |
| `ConnectionStrings__KimiSync` | 必填 | SQL Server 連線字串；JSON 設定路徑為 `ConnectionStrings:KimiSync` |
| `WEBSITE_URL` | `https://example.com` | 允許的 Origin、CORS 來源及首頁轉址目標 |
| `IS_PUBLIC` | 未設定 | 設為 `yes` 可關閉 Origin 限制，仍須使用 Token 存取同步資料 |
| `HTTP_CORS` | 未設定 | 設為 `yes` 允許 `WEBSITE_URL` 的跨來源請求 |
| `HTTPS_REDIRECT` | 未設定 | 設為 `yes` 將 HTTP 請求以 301 轉至 HTTPS |
| `SECRET_PATH` | `secret.key` | UTF-8 HMAC 金鑰檔，至少 2048 字元；建議使用絕對路徑 |
| `ASPNETCORE_URLS` | 依 launch profile | 自訂監聽位址；使用 `dotnet run --no-launch-profile` 套用 |

`IS_PUBLIC` 與 `HTTP_CORS` 分別控制伺服器的 Origin 檢查及瀏覽器 CORS 行為。Origin 檢查不取代 Token 驗證。

## API

| 方法 | 路徑 | 說明 |
| --- | --- | --- |
| GET | `/` | 301 轉至 `WEBSITE_URL` |
| GET | `/robots.txt` | 回傳禁止爬蟲的規則 |
| GET | `/auth/client` | 簽發新的客戶端 Token，回傳 `{ "secret": "..." }` |
| GET | `/sync/data` | 讀取目前客戶端的備份 |
| PUT | `/sync/data` | 新增或覆寫目前客戶端的備份 |

### 取得 Token

```sh
curl -H 'Origin: https://example.com' \
  http://localhost:5036/auth/client
```

每次呼叫都會建立新的客戶端身分。請保存回傳的 `secret`，後續透過 `X-Kumi-Token` 標頭使用同一個 Token。Token 沒有到期時間，持有者即可存取對應資料。

### 寫入備份

將 `<secret>` 替換為取得的 Token：

```sh
curl -X PUT http://localhost:5036/sync/data \
  -H 'Origin: https://example.com' \
  -H 'X-Kumi-Token: <secret>' \
  -H 'Content-Type: application/json' \
  -d '{"content":"backup"}'
```

`content` 必須為非空字串。成功時回傳儲存後的資料：

```json
{
  "_id": "18ea1da5-8bdf-470c-9b04-4a1da5a63239",
  "content": "backup",
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": "2026-01-01T00:00:00Z",
  "__v": 0
}
```

首次寫入會建立資料，後續寫入保留 `createdAt` 並更新 `updatedAt`。時間使用 UTC，寫入使用交易處理併發 upsert。`__v` 在新增時設為 0，不會隨更新遞增。

### 讀取備份

```sh
curl http://localhost:5036/sync/data \
  -H 'Origin: https://example.com' \
  -H 'X-Kumi-Token: <secret>'
```

成功時回傳相同的資料結構。

| 狀態碼 | 情況 |
| --- | --- |
| 400 | PUT 的 JSON 格式錯誤，或 `content` 不是非空字串 |
| 401 | 同步端點未提供有效 Token |
| 403 | Origin 不符合設定 |
| 404 | 該客戶端尚未儲存資料 |
| 500 | 伺服器或資料庫發生錯誤 |

也可使用 [KimiSync.http](KimiSync.http) 手動呼叫 API。

## 建置與部署

```sh
dotnet build
dotnet publish -c Release -o publish
```

部署時提供資料庫連線字串與金鑰檔，並先完成資料表初始化。執行 API 的資料庫帳號需要 `dbo.SyncData` 的 SELECT、INSERT、UPDATE 權限；初始化帳號另需建表權限。

Compose 使用 SQL Server Developer 版本，供開發測試。正式環境請使用適用的 SQL Server 授權、專用資料庫帳號及可信任的 TLS 憑證；範例的 `sa` 與 `TrustServerCertificate=True` 僅供本機開發。

HTTPS 可透過 Kestrel 的端點及憑證設定，或由反向代理提供。代理終止 TLS 時，請由代理負責 HTTP 至 HTTPS 轉址，避免應用程式重複轉址。
