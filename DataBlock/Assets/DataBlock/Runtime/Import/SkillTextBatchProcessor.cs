using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

public enum SkillTextBatchErrorCode
{
    None = 0,
    InvalidSource = 1,
    DuplicateId = 2,
    InvalidSkillText = 3,
    RegexTimeout = 4
}

public sealed class SkillTextSource
{
    public string Id { get; private set; }
    public string Text { get; private set; }

    public SkillTextSource(string id, string text)
    {
        Id = id;
        Text = text;
    }
}

public sealed class SkillTextBatchResult
{
    public string SourceId { get; private set; }
    public SkillCatalogRecord Record { get; private set; }
    public SkillTextBatchErrorCode ErrorCode { get; private set; }
    public string ErrorMessage { get; private set; }
    public bool Succeeded { get { return Record != null; } }

    internal static SkillTextBatchResult Success(string sourceId, SkillTextData data)
    {
        return new SkillTextBatchResult
        {
            SourceId = sourceId,
            Record = new SkillCatalogRecord { Id = sourceId, Data = data },
            ErrorCode = SkillTextBatchErrorCode.None,
            ErrorMessage = ""
        };
    }

    internal static SkillTextBatchResult Failure(string sourceId, SkillTextBatchErrorCode code, string message)
    {
        return new SkillTextBatchResult
        {
            SourceId = sourceId ?? "",
            ErrorCode = code,
            ErrorMessage = message ?? ""
        };
    }
}

// 1件ずつ結果を返すため、全テキストと全エラーを同時にメモリへ保持しません。
public static class SkillTextBatchProcessor
{
    public static IEnumerable<SkillTextBatchResult> Parse(
        IEnumerable<SkillTextSource> sources,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (sources == null) throw new ArgumentNullException(nameof(sources));
        return ParseIterator(sources, cancellationToken);
    }

    private static IEnumerable<SkillTextBatchResult> ParseIterator(
        IEnumerable<SkillTextSource> sources,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SkillTextSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (source == null)
            {
                yield return SkillTextBatchResult.Failure("", SkillTextBatchErrorCode.InvalidSource, "入力元がnullです。");
                continue;
            }

            string id = source.Id;
            SkillTextBatchResult invalidSource = ValidateSource(id);
            if (invalidSource != null)
            {
                yield return invalidSource;
                continue;
            }

            if (!ids.Add(id))
            {
                yield return SkillTextBatchResult.Failure(id, SkillTextBatchErrorCode.DuplicateId, "スキルIDが重複しています：" + id);
                continue;
            }

            yield return ParseSource(source);
        }
    }

    private static SkillTextBatchResult ValidateSource(string id)
    {
        try
        {
            SkillCatalogSharding.ValidateId(id);
            return null;
        }
        catch (InvalidOperationException exception)
        {
            return SkillTextBatchResult.Failure(id, SkillTextBatchErrorCode.InvalidSource, exception.Message);
        }
    }

    private static SkillTextBatchResult ParseSource(SkillTextSource source)
    {
        try
        {
            return SkillTextBatchResult.Success(source.Id, SkillTextConverter.Parse(source.Text));
        }
        catch (RegexMatchTimeoutException exception)
        {
            return SkillTextBatchResult.Failure(source.Id, SkillTextBatchErrorCode.RegexTimeout, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return SkillTextBatchResult.Failure(source.Id, SkillTextBatchErrorCode.InvalidSkillText, exception.Message);
        }
    }
}
