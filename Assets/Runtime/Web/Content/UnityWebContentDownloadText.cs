using Celeste.Localization.Languages;
using Celeste.Localization.Lookup;

namespace Celeste.Unity.Web.Content
{
    internal static class UnityWebContentDownloadText
    {
        private static readonly string[] Keys =
        {
            "section", "cancel", "complete_button", "resume", "retry", "start", "running", "complete",
            "cancelled", "quota", "failed", "idle", "progress", "reused_progress"
        };

        internal static void Apply(DialogCatalog catalog, DialogLanguage language)
        {
            if (language == null) return;
            string[] values = Translations(language.Id);
            for (int i = 0; i < Keys.Length; i++)
            {
                string key = catalog.LookupPolicy.NormalizeKey("web_content_download_" + Keys[i]);
                if (!language.Dialog.ContainsKey(key)) language.Dialog.Add(key, values[i]);
                if (!language.Cleaned.ContainsKey(key)) language.Cleaned.Add(key, language.Dialog[key]);
            }

            string[] rateKeys = { "speed", "eta", "estimating", "reading_cache", "waiting", "saving" };
            string[] rateValues = RateTranslations(language.Id);
            for (int i = 0; i < rateKeys.Length; i++)
            {
                string key = catalog.LookupPolicy.NormalizeKey("web_content_download_" + rateKeys[i]);
                if (!language.Dialog.ContainsKey(key)) language.Dialog.Add(key, rateValues[i]);
                if (!language.Cleaned.ContainsKey(key)) language.Cleaned.Add(key, language.Dialog[key]);
            }

            string unknownKey = catalog.LookupPolicy.NormalizeKey("web_content_download_rate_unknown");
            string unknown = language.Id switch
            {
                "schinese" => "无法获取下载速度",
                "japanese" => "ダウンロード速度を取得できません",
                "korean" => "다운로드 속도를 확인할 수 없습니다",
                "french" => "Débit indisponible",
                "german" => "Downloadrate nicht verfügbar",
                "italian" => "Velocità non disponibile",
                "spanish" => "Velocidad no disponible",
                "brazilian" => "Velocidade indisponível",
                "russian" => "Скорость загрузки недоступна",
                _ => "Download rate unavailable"
            };
            if (!language.Dialog.ContainsKey(unknownKey)) language.Dialog.Add(unknownKey, unknown);
            if (!language.Cleaned.ContainsKey(unknownKey))
                language.Cleaned.Add(unknownKey, language.Dialog[unknownKey]);
        }

        private static string[] RateTranslations(string language) => language switch
        {
            "schinese" => new[] { "下载速度：约 {0}/s", "还需约 {0}", "正在计算所需时间", "读取本地缓存", "等待下载数据", "正在检查与保存文件" },
            "japanese" => new[]
                { "ダウンロード速度：約{0}/s", "残り時間：約{0}", "残り時間を確認中", "ローカルキャッシュを読み込み中", "データを待っています", "ファイルの確認と保存中" },
            "korean" => new[]
                { "다운로드 속도: 약 {0}/s", "남은 시간: 약 {0}", "남은 시간 계산 중", "캐시 읽는 중", "데이터 대기 중", "파일 확인 / 저장 중" },
            "french" => new[]
            {
                "Débit : ~{0}/s", "Temps restant : ~{0}", "Estimation du temps restant", "Lecture du cache local",
                "En attente des données", "Vérification et enregistrement"
            },
            "german" => new[]
            {
                "Downloadrate: ~{0}/s", "Restzeit: ~{0}", "Restzeit wird geschätzt", "Lokaler Cache wird gelesen",
                "Warten auf Daten", "Dateien prüfen und speichern"
            },
            "italian" => new[]
            {
                "Velocità: ~{0}/s", "Tempo restante: ~{0}", "Stima del tempo restante", "Lettura della cache locale",
                "In attesa dei dati", "Verifica e salvataggio dei file"
            },
            "spanish" => new[]
            {
                "Velocidad: ~{0}/s", "Tiempo restante: ~{0}", "Estimando tiempo restante", "Leyendo caché local",
                "Esperando datos", "Verificando y guardando archivos"
            },
            "brazilian" => new[]
            {
                "Velocidade: ~{0}/s", "Tempo restante: ~{0}", "Estimando tempo restante", "Lendo cache local",
                "Aguardando dados", "Verificando e salvando arquivos"
            },
            "russian" => new[]
            {
                "Скорость: ~{0}/s", "Осталось: ~{0}", "Расчёт оставшегося времени", "Чтение локального кэша",
                "Ожидание данных", "Проверка и сохранение файлов"
            },
            _ => new[]
            {
                "Download rate: ~{0}/s", "Time left: ~{0}", "Estimating time left", "Reading local cache",
                "Waiting for download data", "Checking and saving files"
            }
        };

        private static string[] Translations(string language) => language switch
        {
            "schinese" => new[]
            {
                "网页资源", "取消下载", "下载完成", "继续下载", "重试下载", "下载全部资源", "正在下载资源",
                "全部资源已下载", "已取消，缓存资源已保留", "网页保存空间不足，请清理后重试", "下载失败，可以重试",
                "将资源缓存到此网页", "{0}%  已缓存 {1} / {2}  ({3} / {4} 个文件)", "已复用缓存：{0}"
            },
            "japanese" => new[]
            {
                "ゲームデータ", "ダウンロードを中止", "ダウンロード完了", "ダウンロードを再開", "もう一度試す", "すべてのデータをダウンロード",
                "ダウンロード中", "すべてのデータを保存しました", "中止しました。保存済みデータは保持されます", "保存スペースが不足しています",
                "ダウンロード失敗。再試行できます", "ゲームデータを保存", "{0}%  保存済み {1} / {2}  ({3} / {4} ファイル)", "再利用したデータ：{0}"
            },
            "korean" => new[]
            {
                "브라우저 리소스", "다운로드 취소", "다운로드 완료", "다운로드 재개", "다시 시도", "모든 리소스 다운로드", "리소스 다운로드 중",
                "모든 리소스 다운로드 완료", "취소됨. 저장된 리소스는 유지됩니다", "브라우저 저장 공간 부족. 공간을 확보해 주세요", "다운로드 실패. 다시 시도할 수 있습니다",
                "이 브라우저에 리소스 저장", "{0}%  저장됨 {1} / {2}  (파일 {3} / {4})", "재사용한 캐시: {0}"
            },
            "french" => new[]
            {
                "Ressources Web", "Annuler le téléchargement", "Téléchargement terminé", "Reprendre le téléchargement",
                "Réessayer", "Télécharger toutes les ressources",
                "Téléchargement en cours", "Toutes les ressources sont téléchargées", "Annulé ; le cache est conservé",
                "Stockage du navigateur plein ; libérez de l'espace", "Échec du téléchargement ; réessayez",
                "Enregistrer les ressources dans ce navigateur", "{0}%  En cache : {1} / {2}  ({3} / {4} fichiers)",
                "Cache réutilisé : {0}"
            },
            "german" => new[]
            {
                "Web-Ressourcen", "Download abbrechen", "Download abgeschlossen", "Download fortsetzen",
                "Erneut versuchen", "Alle Ressourcen herunterladen", "Ressourcen werden heruntergeladen",
                "Alle Ressourcen heruntergeladen", "Abgebrochen; gespeicherte Daten bleiben erhalten",
                "Browserspeicher voll; bitte Speicher freigeben", "Download fehlgeschlagen; erneut versuchen",
                "Ressourcen in diesem Browser speichern", "{0}%  Gespeichert: {1} / {2}  ({3} / {4} Dateien)",
                "Aus dem Cache: {0}"
            },
            "italian" => new[]
            {
                "Risorse Web", "Annulla download", "Download completato", "Riprendi download", "Riprova",
                "Scarica tutte le risorse", "Download delle risorse in corso",
                "Tutte le risorse sono state scaricate", "Annullato; i dati salvati vengono conservati",
                "Memoria del browser piena; libera spazio", "Download non riuscito; puoi riprovare",
                "Salva le risorse in questo browser", "{0}%  In cache: {1} / {2}  ({3} / {4} file)",
                "Cache riutilizzata: {0}"
            },
            "spanish" => new[]
            {
                "Recursos web", "Cancelar descarga", "Descarga completada", "Reanudar descarga", "Reintentar",
                "Descargar todos los recursos", "Descargando recursos",
                "Todos los recursos descargados", "Cancelado; se conservan los datos guardados",
                "Almacenamiento del navegador lleno; libera espacio", "Error de descarga; puedes reintentarlo",
                "Guardar recursos en este navegador", "{0}%  En caché: {1} / {2}  ({3} / {4} archivos)",
                "Caché reutilizada: {0}"
            },
            "brazilian" => new[]
            {
                "Recursos web", "Cancelar download", "Download concluído", "Retomar download", "Tentar novamente",
                "Baixar todos os recursos", "Baixando recursos",
                "Todos os recursos foram baixados", "Cancelado; os dados salvos foram mantidos",
                "Armazenamento do navegador cheio; libere espaço", "Falha no download; tente novamente",
                "Salvar recursos neste navegador", "{0}%  Em cache: {1} / {2}  ({3} / {4} arquivos)",
                "Cache reutilizado: {0}"
            },
            "russian" => new[]
            {
                "Ресурсы веб-версии", "Отменить загрузку", "Загрузка завершена", "Продолжить загрузку", "Повторить",
                "Загрузить все ресурсы", "Загрузка ресурсов",
                "Все ресурсы загружены", "Отменено, загруженные данные сохранены",
                "Хранилище браузера заполнено, освободите место", "Ошибка загрузки, можно повторить",
                "Сохранить ресурсы в этом браузере", "{0}%  Сохранено: {1} / {2}  ({3} / {4} файлов)", "Из кэша: {0}"
            },
            _ => new[]
            {
                "Web resources", "Cancel download", "Download complete", "Resume download", "Retry download",
                "Download all resources", "Downloading resources",
                "All resources downloaded", "Cancelled; cached resources are kept",
                "Browser storage is full; free space and retry", "Download failed; you can retry",
                "Save resources in this browser", "{0}%  Cached {1} / {2}  ({3} / {4} files)", "Reused from cache: {0}"
            }
        };
    }
}
