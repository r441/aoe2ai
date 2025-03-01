﻿using Language.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Language.ScriptItems
{
    public class Defrule : IScriptItem
    {
        public static int MaxRuleSize = 32;

        public static int MaxLineLength = 255;

        public static readonly IDefruleFormat DefaultFormat = new IndentedDefrule();

        public string Id = Guid.NewGuid().ToString();

        public bool Compressable { get; set; } = true;

        public bool Splittable { get; set; } = true;

        public bool MarkedForDeletion { get; set; } = false;

        public List<Condition> Conditions { get; }

        public List<Action> Actions { get; }

        public IDefruleFormat Format { get; set; } = DefaultFormat;

        public int Length
        {
            get
            {
                return Conditions.Select(x => x.Length).Sum() + LengthOfActions;
            }
        }

        public int LengthOfActions
        {
            get
            {
                return Actions.Select(x => x.Length).Sum();
            }
        }

        public bool IsTooLong => Length > MaxRuleSize;

        public Defrule()
        {
            Conditions = new List<Condition>() { new Condition("true") };
            Actions = new List<Action>() { new Action("do-nothing") };
        }

        public Defrule(IEnumerable<Condition> conditions, IEnumerable<Action> actions)
        {
            Conditions = conditions.ToList();
            Actions = actions.ToList();
        }

        public Defrule(IEnumerable<string> conditions, IEnumerable<string> actions)
        {
            Conditions = conditions.Select(x => new Condition(x)).ToList();
            Actions = actions.Select(x => new Action(x)).ToList();
        }

        public override string ToString()
        {
            return Format.Format(Conditions, Actions).Wrap(MaxLineLength);
        }

        public void Optimize()
        {
            if (Conditions.Count > 1)
            {
                Conditions.RemoveAll(x => x.Text == "true");
                if (!Conditions.Any())
                {
                    Conditions.Add(new Condition("true"));
                }
            }
            if (Actions.Count > 1)
            {
                Actions.RemoveAll(x => x.Text == "do-nothing");
                if (!Actions.Any())
                {
                    Actions.Add(new Action("do-nothing"));
                }
            }
            if (Conditions.Any(x => x.Text == "false"))
            {
                // might not be able to do this due to some conditions changing the state of goals
                // TODO: investigate
                // MarkedForDeletion = true;
            }
            while (Actions.Count(x => x.Text == "disable-self") >= 2)
            {
                Actions.Remove(Actions.First(x => x.Text == "disable-self"));
            }
            if (IsTooLong)
            {
                throw new InvalidOperationException($"Rule is overlength. Length: {Length}, Maximum length: {MaxRuleSize}.");
            }
        }

        public Defrule Split()
        {
            if (!Splittable)
            {
                throw new InvalidOperationException("Rule is not splittable.");
            }
            if (Actions.Count <= 1)
            {
                throw new InvalidOperationException("Not enough actions to split.");
            }

            var applyDisableSelf = Actions.RemoveAll(x => x.Text == "disable-self") >= 1;

            var rule = new Defrule(Conditions.Select(x => x.Copy()), Actions.GetRange(Actions.Count / 2, Actions.Count - Actions.Count / 2));
            Actions.RemoveRange(Actions.Count / 2, Actions.Count - Actions.Count / 2);

            if (applyDisableSelf)
            {
                Actions.Add(new Action("disable-self"));
                rule.Actions.Add(new Action("disable-self"));
            }

            return rule;
        }

        public void MergeIn(Defrule rule)
        {
            if (!Compressable)
            {
                throw new ArgumentException("This rule is not compressable.");
            }
            if (!rule.Compressable)
            {
                throw new ArgumentException("The rule being merged in is not compressable.");
            }
            if (string.Join("", Conditions) != string.Join("", rule.Conditions))
            {
                throw new ArgumentException("The provided rule has different conditions, unable to merge.");
            }
            if (Length + rule.LengthOfActions > MaxRuleSize)
            {
                throw new ArgumentException("The provided rule has too many actions to be merged.");
            }
            if (Actions.Any(x => x.Text == "disable-self") != rule.Actions.Any(x => x.Text == "disable-self"))
            {
                throw new ArgumentException("Rules with the action 'disable-self' cannot be merged.");
            }
            Actions.AddRange(rule.Actions);
            rule.Actions.Clear();
            rule.Actions.Add(new Action("do-nothing"));
        }

        public bool CanMergeWith(Defrule rule)
        {
            return string.Join("", Conditions) == string.Join("", rule.Conditions)
                && Length + rule.LengthOfActions <= MaxRuleSize
                && Actions.Any(x => x.Text == "disable-self") == rule.Actions.Any(x => x.Text == "disable-self")
                && Compressable
                && rule.Compressable;
        }
    }
}
