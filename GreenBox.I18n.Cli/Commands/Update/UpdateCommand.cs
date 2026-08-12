using System.CommandLine;
using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class UpdateCommand
{
    public static Command Create()
    {
        var catalogArgument = new Argument<FileInfo>("catalog")
        {
            Description = "Path to the i18n catalog JSON file.",
        };
        var idArgument = new Argument<long>("id")
        {
            Description = "Positive numeric ID of the entry.",
        };
        var commentOption = new Option<string?>("--comment")
        {
            Description = "Set the developer and translator comment.",
        };
        var clearCommentOption = new Option<bool>("--clear-comment")
        {
            Description = "Remove the comment.",
        };
        var localeOption = new Option<string?>("--locale")
        {
            Description = "Locale ID affected by text or asset options.",
        };
        var textOption = new Option<string?>("--text")
        {
            Description = "Set localized text. An empty string remains an explicit value.",
        };
        var clearTextOption = new Option<bool>("--clear-text")
        {
            Description = "Remove localized text for --locale.",
        };
        var assetGuidOption = new Option<string?>("--asset-guid")
        {
            Description = "Set a Unity asset reference using its 32-character GUID.",
        };
        var assetLocalIdOption = new Option<string?>("--asset-local-id")
        {
            Description = "Optional Unity local file ID used with --asset-guid.",
        };
        var clearAssetOption = new Option<bool>("--clear-asset")
        {
            Description = "Remove the asset reference for --locale.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON result.",
        };
        var command = new Command("update", "Update comment, localized text, or an asset reference for one entry.")
        {
            Arguments = { catalogArgument, idArgument },
            Options =
            {
                commentOption,
                clearCommentOption,
                localeOption,
                textOption,
                clearTextOption,
                assetGuidOption,
                assetLocalIdOption,
                clearAssetOption,
                jsonOption,
            },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(catalogArgument)!,
            parseResult.GetValue(idArgument),
            new UpdateRequest
            {
                HasComment = parseResult.GetResult(commentOption) != null,
                Comment = parseResult.GetValue(commentOption),
                ClearComment = parseResult.GetValue(clearCommentOption),
                Locale = parseResult.GetValue(localeOption),
                HasText = parseResult.GetResult(textOption) != null,
                Text = parseResult.GetValue(textOption),
                ClearText = parseResult.GetValue(clearTextOption),
                HasAssetGuid = parseResult.GetResult(assetGuidOption) != null,
                AssetGuid = parseResult.GetValue(assetGuidOption),
                HasAssetLocalId = parseResult.GetResult(assetLocalIdOption) != null,
                AssetLocalId = parseResult.GetValue(assetLocalIdOption),
                ClearAsset = parseResult.GetValue(clearAssetOption),
            },
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        FileInfo catalogFile,
        long id,
        UpdateRequest request,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        string? requestError = ValidateRequest(request);
        if (requestError != null)
        {
            return CommandOutput.WriteError(
                catalogFile,
                CliDiagnosticCodes.InvalidUpdate,
                requestError,
                CliExitCodes.ExecutionError,
                writeJson,
                standardOutput,
                standardError);
        }

        CatalogLoadResult loadResult = CatalogLoader.Load(catalogFile);
        if (loadResult.Catalog == null)
        {
            return CommandOutput.WriteLoadError(
                catalogFile,
                loadResult,
                writeJson,
                standardOutput,
                standardError);
        }

        I18nCatalog catalog = loadResult.Catalog;
        bool changed = false;

        if (request.HasComment || request.ClearComment)
        {
            I18nEditResult result = catalog.SetEntryComment(id, request.ClearComment ? null : request.Comment);
            int? failure = HandleEditFailure(result, catalogFile, writeJson, standardOutput, standardError);
            if (failure != null)
            {
                return failure.Value;
            }

            changed |= result.HasChanges;
        }

        if (request.HasText || request.ClearText)
        {
            I18nEditResult result = catalog.SetEntryText(
                id,
                request.Locale,
                request.ClearText ? null : request.Text);
            int? failure = HandleEditFailure(result, catalogFile, writeJson, standardOutput, standardError);
            if (failure != null)
            {
                return failure.Value;
            }

            changed |= result.HasChanges;
        }

        if (request.HasAssetGuid || request.ClearAsset)
        {
            I18nAssetReference? asset = request.ClearAsset
                ? null
                : new I18nAssetReference
                {
                    AssetGuid = request.AssetGuid!,
                    LocalFileId = request.HasAssetLocalId ? request.AssetLocalId : null,
                };
            I18nEditResult result = catalog.SetEntryAsset(id, request.Locale, asset);
            int? failure = HandleEditFailure(result, catalogFile, writeJson, standardOutput, standardError);
            if (failure != null)
            {
                return failure.Value;
            }

            changed |= result.HasChanges;
        }

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
        if (validation.HasErrors)
        {
            I18nValidationDiagnostic diagnostic = validation.Diagnostics.First(
                item => item.Severity == I18nValidationSeverity.Error);
            return CommandOutput.WriteError(
                catalogFile,
                diagnostic.Code,
                diagnostic.Message,
                CliExitCodes.InvalidData,
                writeJson,
                standardOutput,
                standardError);
        }

        if (changed)
        {
            CliError? writeError = CatalogFileWriter.WriteAtomically(
                catalogFile,
                I18nCatalogJson.Serialize(catalog));
            if (writeError != null)
            {
                return CommandOutput.WriteError(
                    catalogFile,
                    writeError.Code,
                    writeError.Message,
                    CliExitCodes.ExecutionError,
                    writeJson,
                    standardOutput,
                    standardError);
            }
        }

        I18nEntry entry = catalog.FindById(id)!;
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new UpdateReport
            {
                File = catalogFile.FullName,
                Changed = changed,
                Entry = EntryReport.Create(entry),
            }));
        }
        else
        {
            standardOutput.WriteLine(changed ? "Updated." : "Unchanged.");
            CommandOutput.WriteEntry(entry, standardOutput);
        }

        return CliExitCodes.Success;
    }

    private static string? ValidateRequest(UpdateRequest request)
    {
        bool updatesComment = request.HasComment || request.ClearComment;
        bool updatesText = request.HasText || request.ClearText;
        bool updatesAsset = request.HasAssetGuid || request.ClearAsset;

        if (!updatesComment && !updatesText && !updatesAsset && !request.HasAssetLocalId)
        {
            return "Specify at least one value to update.";
        }

        if (request.HasComment && request.ClearComment)
        {
            return "--comment and --clear-comment cannot be used together.";
        }

        if (request.HasText && request.ClearText)
        {
            return "--text and --clear-text cannot be used together.";
        }

        if (request.HasAssetGuid && request.ClearAsset)
        {
            return "--asset-guid and --clear-asset cannot be used together.";
        }

        if (request.HasAssetLocalId && !request.HasAssetGuid)
        {
            return "--asset-local-id requires --asset-guid.";
        }

        if ((updatesText || updatesAsset) && string.IsNullOrWhiteSpace(request.Locale))
        {
            return "--locale is required when updating text or an asset reference.";
        }

        return null;
    }

    private static int? HandleEditFailure(
        I18nEditResult result,
        FileInfo catalogFile,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (result.IsSuccess)
        {
            return null;
        }

        I18nEditError error = result.Error!;
        return CommandOutput.WriteError(
            catalogFile,
            error.Code,
            error.Message,
            CliExitCodes.InvalidData,
            writeJson,
            standardOutput,
            standardError);
    }
}

internal sealed class UpdateRequest
{
    public bool HasComment { get; init; }

    public string? Comment { get; init; }

    public bool ClearComment { get; init; }

    public string? Locale { get; init; }

    public bool HasText { get; init; }

    public string? Text { get; init; }

    public bool ClearText { get; init; }

    public bool HasAssetGuid { get; init; }

    public string? AssetGuid { get; init; }

    public bool HasAssetLocalId { get; init; }

    public string? AssetLocalId { get; init; }

    public bool ClearAsset { get; init; }
}
