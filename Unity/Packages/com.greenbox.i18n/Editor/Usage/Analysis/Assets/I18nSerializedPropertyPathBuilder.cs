#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace GreenBox.I18n.Usage.Analysis
{
    internal sealed class I18nSerializedPropertyPathBuilder
    {
        private const string EntryIdPropertyName = "_greenBoxI18nEntryId";
        private readonly List<PathNode> _nodes = new();

        public void Reset()
        {
            _nodes.Clear();
        }

        public string Process(int indentation, string key, string scalarValue, bool isSequenceItem)
        {
            if (isSequenceItem)
            {
                while (_nodes.Count > 0 && _nodes[_nodes.Count - 1].Indentation > indentation)
                {
                    _nodes.RemoveAt(_nodes.Count - 1);
                }

                if (_nodes.Count > 0)
                {
                    PathNode parent = _nodes[_nodes.Count - 1];
                    int itemIndex = parent.NextSequenceIndex++;
                    _nodes.Add(new PathNode(indentation + 1, $"Array.data[{itemIndex}]"));
                }
            }
            else
            {
                while (_nodes.Count > 0 && _nodes[_nodes.Count - 1].Indentation >= indentation)
                {
                    _nodes.RemoveAt(_nodes.Count - 1);
                }
            }

            string path = string.Join(".", _nodes.Select(node => node.Name).Append(key));
            if (scalarValue.Length == 0 && key != EntryIdPropertyName)
            {
                _nodes.Add(new PathNode(isSequenceItem ? indentation + 1 : indentation, key));
            }

            return path;
        }

        private sealed class PathNode
        {
            public PathNode(int indentation, string name)
            {
                Indentation = indentation;
                Name = name;
            }

            public int Indentation { get; }
            public string Name { get; }
            public int NextSequenceIndex { get; set; }
        }
    }
}
