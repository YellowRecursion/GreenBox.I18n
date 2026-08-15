#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;
using NUnit.Framework;

namespace GreenBox.I18n.Unity.Editor.Tests
{
    public sealed class I18nUnityYamlParserTests
    {
        private const long EntryId = 3857505375280397613;

        [Test]
        public void Parse_IndexesI18nKeyInOdinSerializationNodes()
        {
            I18nSerializedAssetModel model = Parse(
                @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_GameObject: {fileID: 0}
  m_Name: Mission Repository
  serializationData:
    SerializedFormat: 2
    SerializationNodes:
    - Name: _greenBoxI18nEntryId
      Entry: 3
      Data: 3857505375280397613
");

            I18nSerializedUsageCandidate candidate = model.Candidates.Single();
            Assert.That(candidate.EntryId, Is.EqualTo(EntryId));
            Assert.That(candidate.DocumentLocalId, Is.EqualTo(11400000));
            Assert.That(
                candidate.PropertyPath,
                Is.EqualTo(
                    "serializationData.SerializationNodes.Array.data[0]._greenBoxI18nEntryId"));
        }

        [Test]
        public void Parse_DoesNotTreatArbitraryNameAndDataListAsOdinI18nKey()
        {
            I18nSerializedAssetModel model = Parse(
                @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_GameObject: {fileID: 0}
  m_Name: Other Asset
  customNodes:
  - Name: _greenBoxI18nEntryId
    Entry: 3
    Data: 3857505375280397613
");

            Assert.That(model.Candidates, Is.Empty);
        }

        private static I18nSerializedAssetModel Parse(string yaml)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"greenbox-i18n-yaml-{Guid.NewGuid():N}.asset");
            try
            {
                File.WriteAllText(path, yaml);
                var warnings = new List<string>();
                I18nSerializedAssetModel model = I18nUnityYamlParser.Parse(
                    path,
                    "Assets/Test.asset",
                    "0123456789abcdef0123456789abcdef",
                    warnings);
                Assert.That(warnings, Is.Empty);
                return model;
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
