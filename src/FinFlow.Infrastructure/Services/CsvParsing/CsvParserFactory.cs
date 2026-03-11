using System.Text;
using FinFlow.Domain.Interfaces;

namespace FinFlow.Infrastructure.Services.CsvParsing;

/// <summary>
/// CSVヘッダー行からフォーマットを判定し、適切なICsvParserを返すファクトリー
/// 新しい銀行フォーマットの追加は、このクラスを変更せずにICsvParserを実装し
/// コンストラクタに注入することで可能（OCP: 開放閉鎖原則）
/// </summary>
public class CsvParserFactory
{
    private readonly IEnumerable<ICsvParser> _parsers;
    private readonly GenericCsvParser _genericParser;

    public CsvParserFactory(IEnumerable<ICsvParser> parsers)
    {
        _parsers = parsers;
        _genericParser = new GenericCsvParser();
    }

    /// <summary>
    /// CSVストリームのヘッダー行を読み取り、対応するパーサーを返す。
    /// 対応するパーサーが見つからない場合はGenericCsvParserを返す（フォールバック）。
    /// </summary>
    public ICsvParser SelectParser(Stream csvStream, string encoding = "utf-8")
    {
        var headerLine = ReadHeaderLine(csvStream, encoding);

        // ストリームを先頭に巻き戻す（後続のParseで再度先頭から読む必要があるため）
        if (csvStream.CanSeek)
            csvStream.Seek(0, SeekOrigin.Begin);

        return FindMatchingParser(headerLine);
    }

    private ICsvParser FindMatchingParser(string headerLine)
    {
        // 登録済みパーサーを順番に試す（特化パーサーが汎用より優先されるよう順序を意識）
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(headerLine))
                return parser;
        }

        // フォールバック: 汎用パーサー
        return _genericParser;
    }

    private static string ReadHeaderLine(Stream csvStream, string encoding)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var encodingInstance = encoding.ToLowerInvariant() switch
        {
            "shift_jis" or "shift-jis" or "sjis" or "cp932" => Encoding.GetEncoding("shift_jis"),
            _ => Encoding.UTF8
        };

        // ストリームの現在位置を記憶し、ヘッダー読み取り後に戻す
        var originalPosition = csvStream.CanSeek ? csvStream.Position : 0;

        using var reader = new StreamReader(csvStream, encodingInstance, leaveOpen: true);
        var headerLine = reader.ReadLine() ?? string.Empty;

        if (csvStream.CanSeek)
            csvStream.Seek(originalPosition, SeekOrigin.Begin);

        return headerLine;
    }
}
