﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace RepoMan.Actions;

public class File : IRunnerItem
{
    private const string ModeAdd = "add";
    private const string ModeChanged = "changed";
    private const string ModeDelete = "remove";

    private string _mode;
    private bool _isValid;
    private IEnumerable<FileCheck> _items;

    public File(YamlSequenceNode node, string mode, State state)
    {
        state.Logger.LogDebug($"BUILD: Check-files with mode {mode}");

        _mode = mode;

        if (mode != ModeAdd && mode != ModeChanged && mode != ModeDelete)
            throw new Exception($"BUILD: File action mode is invalid: {mode}");

        List<FileCheck> items = new List<FileCheck>(node.Children.Count);

        foreach (var item in node.Children)
        {
            state.Logger.LogDebug($"BUILD: Adding check {item["path"]}");
            items.Add(new FileCheck(item["path"].ToString(), Runner.Build(item["run"].AsSequenceNode(), state)));
        }

        _items = items;

        _isValid = true;
    }

    public async Task Run(State state)
    {
        if (!_isValid)
        {
            state.Logger.LogError("File action is invalid, can't run");
            return;
        }

        state.Logger.LogInformation("Running files action and checking for PR file matches");

        // TODO: New feature, detect add/updated/delete file changes.
        // Currently we don't care what happened.
        foreach (var item in _items)
        {
            var match = false;
            foreach (var file in state.PullRequestFiles)
            {
                if (Utilities.MatchRegex(item.RegexCheck, file.FileName ?? "", state) || Utilities.MatchRegex(item.RegexCheck, file.PreviousFileName ?? "", state))
                {
                    state.Logger.LogInformation($"Found a match for {item.RegexCheck}");
                    match = true;
                    break;
                }
            }

            if (match)
                await item.Actions.Run(state);
        }
        return;
    }

    private class FileCheck
    {
        public string RegexCheck;
        public Runner Actions;

        public FileCheck(string regex, Runner actions) =>
            (RegexCheck, Actions) = (regex, actions);
    }
}
