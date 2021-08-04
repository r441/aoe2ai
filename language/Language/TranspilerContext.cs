﻿using Language.ScriptItems;
using System.Collections.Generic;
using System.Linq;

namespace Language
{
    public class TranspilerContext
    {
        public Stack<Condition> ConditionStack { get; set; } = new Stack<Condition>();

        public Stack<Action> ActionStack { get; set; } = new Stack<Action>();

        public Stack<object> DataStack { get; set; } = new Stack<object>();

        public List<IScriptItem> Script { get; set; } = new List<IScriptItem>();

        public string CurrentPath { get; set; }

        public string CurrentFileName { get; set; }

        public IScriptItem ApplyStacks(IScriptItem item)
        {
            if (item is Defrule)
            {
                ((Defrule)item).Conditions.AddRange(ConditionStack.Select(x => x.Copy()));
                ((Defrule)item).Actions.AddRange(ActionStack.Select(x => x.Copy()));
            }
            return item;
        }

        public void AddToScriptWithJump(IEnumerable<IScriptItem> items, Condition skipCondition)
        {
            AddToScript(new Defrule(
                new[] { skipCondition },
                new[] { new Action($"up-jump-rule {items.Count(x => x is Defrule)}") })
            { Compressable = false, Splittable = false });

            foreach (var rule in items)
            {
                if (rule is Defrule)
                {
                    ((Defrule)rule).Compressable = false;
                    ((Defrule)rule).Splittable = false;
                }
            }

            AddToScript(items);
        }

        public void AddToScript(IEnumerable<IScriptItem> items)
        {
            foreach (var item in items)
            {
                AddToScript(item);
            }
        }

        public void AddToScript(IScriptItem item)
        {
            Script.Add(item);
        }

        public void OptimizeScript()
        {
            foreach (var item in Script)
            {
                item.Optimize();
            }

            Script.RemoveAll(x => x.MarkedForDeletion);

            var i = 0;
            while (i < Script.Count - 1)
            {
                var rule = Script[i] as Defrule;
                var otherRule = Script[i + 1] as Defrule;
                if (rule != null && otherRule != null)
                {
                    if (rule.CanMergeWith(otherRule))
                    {
                        rule.MergeIn(otherRule);
                        rule.Optimize();
                        Script.RemoveAt(i + 1);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }
    }
}
