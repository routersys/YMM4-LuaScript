using System.Collections.Generic;
using YukkuriMovieMaker.Controls.AvalonEdit.ToolBarStrategy;

namespace LuaScript
{
    internal sealed class EmptyToolBarStrategy : IToolBarStrategy
    {
        public IEnumerable<ToolBarGroup> GetToolBarGroups() => [];
    }
}
