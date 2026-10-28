using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Playback;
using MacroStudio.Engine.Playback;

namespace MacroStudio.Engine.Tests.Playback;

public sealed class MacroPlayerTests
{
    private static readonly WindowTarget ExcelTarget = new("EXCEL", "Liste.xlsx - Excel", "XLMAIN", new ScreenRect(0, 0, 1000, 800));
    private static readonly WindowTarget SapTarget = new("saplogon", "SAP Easy Access", "SAP_FRONTEND_SESSION", new ScreenRect(0, 0, 1000, 800));

    private static readonly WindowCandidate ExcelWindow = new(1, "EXCEL", "XLMAIN", "Liste.xlsx - Excel", new ScreenRect(0, 0, 1000, 800));
    private static readonly WindowCandidate SapWindow = new(2, "saplogon", "SAP_FRONTEND_SESSION", "SAP Easy Access", new ScreenRect(0, 0, 1000, 800));

    /// <summary>The master prompt's example: copy a cell, paste it into the other program, next row, until the list ends.</summary>
    private static readonly MacroStep[] CopyPasteNextRow =
    [
        new CopyStep { Target = ExcelTarget },
        new FocusWindowStep { Target = SapTarget, DelayBeforeMs = 200 },
        new PasteStep { Target = SapTarget, DelayBeforeMs = 100 },
        new KeyPressStep { VirtualKey = VirtualKeys.Return, Target = SapTarget, DelayBeforeMs = 100 },
        new FocusWindowStep { Target = ExcelTarget, DelayBeforeMs = 300 },
        new KeyPressStep { VirtualKey = VirtualKeys.Down, IsExtended = true, Target = ExcelTarget, DelayBeforeMs = 100 },
    ];

    [Fact]
    public void UntilClipboardEmpty_PastesEveryRowAndStopsAtTheFirstEmptyCell()
    {
        var desktop = Desktop("Kod001\r\n", "Kod002\r\n", "Kod003\r\n", "\r\n");

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions());

        Assert.Equal(PlaybackOutcome.ListEnded, result.Outcome);
        Assert.Equal(3, result.CompletedRounds);
        Assert.Equal(["Kod001", "Kod002", "Kod003"], desktop.Pasted);
    }

    [Fact]
    public void Count_PlaysExactlyThatManyRounds()
    {
        var desktop = Desktop("1", "2", "3", "4", "5");

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions { Loop = LoopSettings.Times(2) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal(["1", "2"], desktop.Pasted);
    }

    [Fact]
    public void UntilClipboardEmpty_WithoutACopyStep_PlaysOnce()
    {
        var desktop = Desktop();
        MacroStep[] steps = [new TypeTextStep { Text = "ab", Target = SapTarget }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions());

        Assert.Equal((PlaybackOutcome.Completed, 1), (result.Outcome, result.CompletedRounds));
        Assert.Equal(["a", "b"], desktop.Typed);
    }

    [Theory]
    [InlineData(1.0, 1000)]
    [InlineData(2.0, 500)]
    [InlineData(0.5, 2000)]
    public void Speed_DividesTheRecordedWaits(double speed, long expectedMs)
    {
        var desktop = Desktop();
        MacroStep[] steps = [new WaitStep { DurationMs = 1000 }];

        Play(desktop, steps, new PlaybackOptions { Speed = speed, Loop = LoopSettings.Times(1) });

        Assert.Equal(expectedMs, desktop.NowMs);
    }

    [Fact]
    public void Click_InAWindowThatMoved_FollowsTheWindow()
    {
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow with { Bounds = new ScreenRect(300, 200, 1300, 1000) });
        MacroStep[] steps = [new MouseClickStep { Button = MouseButton.Left, Target = SapTarget, ScreenPosition = new ScreenPoint(100, 50) }];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal([new ScreenPoint(400, 250)], desktop.Clicks);
        Assert.Empty(desktop.Moves);
    }

    [Fact]
    public void Click_InAWindowOfAnotherSize_PutsTheWindowBackFirst()
    {
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow with { Bounds = new ScreenRect(300, 200, 900, 700) });
        MacroStep[] steps = [new MouseClickStep { Button = MouseButton.Left, Target = SapTarget, ScreenPosition = new ScreenPoint(950, 750) }];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal([SapWindow.Handle], desktop.Moves);
        Assert.Equal(SapTarget.Bounds, desktop.Windows.Single().Bounds);
        Assert.Equal([new ScreenPoint(950, 750)], desktop.Clicks);
    }

    [Fact]
    public void StepsInWindowsSearch_DoNotWaitForItAsAWindow()
    {
        var search = new WindowTarget("SearchApp", "Arama", "Windows.UI.Core.CoreWindow", new ScreenRect(48, 560, 832, 1040));
        var desktop = new FakeDesktop();
        MacroStep[] steps = [new FocusWindowStep { Target = search }, new TypeTextStep { Text = "hi", Target = search }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal(["h", "i"], desktop.Typed);
        Assert.Empty(desktop.Activations);
    }

    [Fact]
    public void BringToFront_OfAHiddenShellHostWindow_IsSkipped()
    {
        var shellHost = new WindowTarget("explorer", "", "ApplicationManager_DesktopShellWindow", new ScreenRect(0, 0, 0, 0));
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow);
        MacroStep[] steps = [new FocusWindowStep { Target = shellHost }, new FocusWindowStep { Target = SapTarget }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal([SapWindow.Handle], desktop.Activations);
    }

    private static readonly UiElement SaveButton = new("", "Kaydet", "ControlType.Button", "Button", 0.5, 0.5);

    [Fact]
    public void Click_OnANamedControlThatMoved_ClicksTheControlWhereItIsNow()
    {
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow);
        desktop.Controls["Kaydet"] = new ScreenRect(700, 500, 781, 531);
        MacroStep[] steps = [new MouseClickStep { Button = MouseButton.Left, Target = SapTarget, ScreenPosition = new ScreenPoint(100, 50), Element = SaveButton }];

        PlaybackResult result = new MacroPlayer(desktop, desktop, desktop, desktop, desktop)
            .Play(Macro(steps), new PlaybackOptions { Loop = LoopSettings.Times(1) }, CancellationToken.None);

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal([new ScreenPoint(740, 515)], desktop.Clicks);
    }

    [Fact]
    public void Click_OnANamedControlThatIsGone_ClicksTheRecordedSpot_AndLooksOnlyOnce()
    {
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow);
        MacroStep[] steps = [new MouseClickStep { Button = MouseButton.Left, Target = SapTarget, ScreenPosition = new ScreenPoint(100, 50), Element = SaveButton }];

        new MacroPlayer(desktop, desktop, desktop, desktop, desktop)
            .Play(Macro(steps), new PlaybackOptions { Loop = LoopSettings.Times(3) }, CancellationToken.None);

        Assert.Equal([new ScreenPoint(100, 50), new ScreenPoint(100, 50), new ScreenPoint(100, 50)], desktop.Clicks);
        Assert.Equal(1, desktop.ControlSearches);
    }

    [Fact]
    public void Retry_AfterAFailedStep_GoesOnWhenTheWindowCameLate()
    {
        var desktop = Desktop();
        desktop.Windows.Remove(SapWindow);
        desktop.OnWait = now =>
        {
            if (now >= 1500 && !desktop.Windows.Contains(SapWindow))
            {
                desktop.Windows.Add(SapWindow);
            }
        };
        var options = new PlaybackOptions { Loop = LoopSettings.Times(1), WindowWait = TimeSpan.FromMilliseconds(500), OnError = new ErrorPolicy(ErrorAction.Stop, 2) };

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "a", Target = SapTarget }], options);

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal(["a"], desktop.Typed);
    }

    [Fact]
    public void SkipStep_LeavesOutTheFailingStepInEveryRound()
    {
        var desktop = Desktop();
        desktop.Windows.Remove(SapWindow);
        MacroStep[] steps = [new TypeTextStep { Text = "a", Target = SapTarget }, new TypeTextStep { Text = "b", Target = ExcelTarget }];
        var options = new PlaybackOptions { Loop = LoopSettings.Times(2), WindowWait = TimeSpan.FromMilliseconds(100), OnError = new ErrorPolicy(ErrorAction.SkipStep, 1) };

        PlaybackResult result = Play(desktop, steps, options);

        Assert.Equal((PlaybackOutcome.Completed, 2, 2), (result.Outcome, result.CompletedRounds, result.SkippedSteps));
        Assert.Equal(["b", "b"], desktop.Typed);
    }

    [Fact]
    public void SkipRound_GoesToTheNextRound_ButStopsWhenRoundsFailInARow()
    {
        var desktop = Desktop("1", "2", "3", "4", "5", "6");
        desktop.Windows.Remove(SapWindow);
        var options = new PlaybackOptions { Loop = LoopSettings.Times(2), WindowWait = TimeSpan.FromMilliseconds(100), OnError = new ErrorPolicy(ErrorAction.SkipRound, 0) };

        PlaybackResult twoRounds = Play(desktop, CopyPasteNextRow, options);
        PlaybackResult manyRounds = Play(desktop, CopyPasteNextRow, options with { Loop = LoopSettings.Times(5) });

        Assert.Equal((PlaybackOutcome.Completed, 0, 2), (twoRounds.Outcome, twoRounds.CompletedRounds, twoRounds.SkippedRounds));
        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.WindowNotFound), (manyRounds.Outcome, manyRounds.Failure));
        Assert.Empty(desktop.Pasted);
    }

    [Fact]
    public void AdministratorProgram_IsNeverSkipped()
    {
        var desktop = Desktop("x");
        desktop.ElevatedWindows.Add(SapWindow.Handle);
        var options = new PlaybackOptions { OnError = new ErrorPolicy(ErrorAction.SkipStep, 2) };

        PlaybackResult result = Play(desktop, CopyPasteNextRow, options);

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.TargetIsElevated), (result.Outcome, result.Failure));
    }

    [Fact]
    public void WaitFor_WindowCloses_GoesOnAsSoonAsItIsGone()
    {
        var desktop = Desktop();
        desktop.OnWait = now =>
        {
            if (now >= 2_000)
            {
                desktop.Windows.Remove(SapWindow);
            }
        };
        MacroStep[] steps = [new WaitForStep { Condition = WaitCondition.WindowCloses, Target = SapTarget }, new TypeTextStep { Text = "a", Target = ExcelTarget }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.InRange(desktop.NowMs, 2_000, 2_300);
    }

    [Fact]
    public void WaitFor_Color_WatchesTheSpotOfAWindowThatMoved()
    {
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow with { Bounds = new ScreenRect(300, 200, 1300, 1000) });
        desktop.OnWait = now =>
        {
            if (now >= 1_000)
            {
                desktop.Pixels[new ScreenPoint(400, 250)] = 0x10A010;
            }
        };
        MacroStep[] steps = [new WaitForStep { Condition = WaitCondition.ColorAppears, Target = SapTarget, ScreenPosition = new ScreenPoint(100, 50), Color = 0x00A000 }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.InRange(desktop.NowMs, 1_000, 1_200);
    }

    [Fact]
    public void WaitFor_ThatNeverComes_FailsAfterItsLongestTime()
    {
        var desktop = Desktop();
        MacroStep[] steps = [new WaitForStep { Condition = WaitCondition.WindowCloses, Target = SapTarget, TimeoutMs = 5_000 }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1), Speed = 5 });

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.WaitTimedOut), (result.Outcome, result.Failure));
        Assert.InRange(desktop.NowMs, 5_000, 5_200);
    }

    [Fact]
    public void ListRows_TypeOneRowPerRound_AndTheListEndingEndsTheMacro()
    {
        var desktop = Desktop();
        MacroStep[] steps = [new TypeTextStep { Text = "{1}:{Tutar};", Target = SapTarget }];
        var options = new PlaybackOptions
        {
            ListRows = [["K1", "10"], ["K2", "20"]],
            ListColumns = ["Kod", "Tutar"],
        };

        PlaybackResult result = Play(desktop, steps, options);

        Assert.Equal((PlaybackOutcome.ListEnded, 2), (result.Outcome, result.CompletedRounds));
        Assert.Equal("K1:10;K2:20;", string.Concat(desktop.Typed));
    }

    [Fact]
    public void ListRows_WithACountSmallerThanTheList_StopsAtTheCount()
    {
        var desktop = Desktop();
        var options = new PlaybackOptions { Loop = LoopSettings.Times(1), ListRows = [["a"], ["b"]] };

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "{1}", Target = SapTarget }], options);

        Assert.Equal((PlaybackOutcome.Completed, 1), (result.Outcome, result.CompletedRounds));
        Assert.Equal(["a"], desktop.Typed);
    }

    [Fact]
    public void If_WindowNotOpen_SkipsTheStepThatNeedsIt_OnlyInTheRoundsWhereItIsMissing()
    {
        var desktop = Desktop();
        var dialog = new WindowTarget("saplogon", "Uyarı", "#32770", new ScreenRect(0, 0, 300, 200));
        var dialogWindow = new WindowCandidate(9, "saplogon", "#32770", "Uyarı", new ScreenRect(0, 0, 300, 200));
        desktop.OnWait = now =>
        {
            // The warning shows up only during the second round.
            if (desktop.Typed.Count == 1 && !desktop.Windows.Contains(dialogWindow))
            {
                desktop.Windows.Insert(0, dialogWindow);
            }
        };
        MacroStep[] steps =
        [
            new IfStep { Check = IfCheck.WindowNotOpen, Target = dialog, Action = IfAction.SkipSteps },
            new KeyPressStep { VirtualKey = VirtualKeys.Return, Target = dialog },
            new TypeTextStep { Text = "x", Target = SapTarget, DelayBeforeMs = 10 },
        ];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(2) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal(["x", "x"], desktop.Typed);
        Assert.Equal([SapWindow.Handle, dialogWindow.Handle, SapWindow.Handle], desktop.Activations);
    }

    [Theory]
    [InlineData(IfAction.EndRound, PlaybackOutcome.Completed, 3, "a,a,a")]
    [InlineData(IfAction.StopMacro, PlaybackOutcome.Completed, 1, "a")]
    public void If_ColorIs_EndsTheRoundOrTheMacro(IfAction action, PlaybackOutcome outcome, int rounds, string typed)
    {
        var desktop = Desktop();
        desktop.Pixels[new ScreenPoint(10, 10)] = 0xFF0000;
        MacroStep[] steps =
        [
            new TypeTextStep { Text = "a", Target = SapTarget },
            new IfStep { Check = IfCheck.ColorIs, ScreenPosition = new ScreenPoint(10, 10), Color = 0xFF0000, Action = action },
            new TypeTextStep { Text = "b", Target = SapTarget },
        ];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(3) });

        Assert.Equal((outcome, rounds), (result.Outcome, result.CompletedRounds));
        Assert.Equal(typed, string.Join(",", desktop.Typed));
    }

    [Fact]
    public void ImageClick_ClicksTheMiddleOfThePictureWhereItIsInTheWindow()
    {
        var random = new Random(7);
        var screenPixels = new byte[1400 * 1100 * 4];
        random.NextBytes(screenPixels);
        var screen = new Bgra32Image(1400, 1100, screenPixels);
        var picturePixels = new byte[20 * 10 * 4];
        random.NextBytes(picturePixels);
        for (int y = 0; y < 10; y++)
        {
            Array.Copy(picturePixels, y * 80, screenPixels, (((700 + y) * 1400) + 900) * 4, 80);
        }

        var desktop = new FakeDesktop { Screen = screen };
        desktop.Windows.Add(SapWindow with { Bounds = new ScreenRect(300, 200, 1300, 1000) });
        var target = SapTarget with { Bounds = new ScreenRect(300, 200, 1300, 1000) };
        MacroStep[] steps = [new ImageClickStep { ImageWidth = 20, ImageHeight = 10, ImagePixels = picturePixels, Target = target }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal([new ScreenPoint(910, 705)], desktop.Clicks);
    }

    [Fact]
    public void ImageClick_ThatIsNotOnTheScreen_FailsAfterItsTime()
    {
        var desktop = new FakeDesktop { Screen = new Bgra32Image(100, 100, new byte[100 * 100 * 4]) };
        var picture = new byte[10 * 10 * 4];
        Array.Fill(picture, (byte)200);
        MacroStep[] steps = [new ImageClickStep { ImageWidth = 10, ImageHeight = 10, ImagePixels = picture, TimeoutMs = 1_000 }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.ImageNotFound), (result.Outcome, result.Failure));
        Assert.Empty(desktop.Clicks);
    }

    [Fact]
    public void OpenStep_OpensItAndTheNextStepWaitsForItsWindow()
    {
        var desktop = new FakeDesktop();
        desktop.OnWait = now =>
        {
            // The program's window appears a moment after it was started.
            if (now >= 300 && desktop.Windows.Count == 0)
            {
                desktop.Windows.Add(SapWindow);
            }
        };
        MacroStep[] steps = [new OpenStep { Path = "saplogon" }, new TypeTextStep { Text = "a", Target = SapTarget }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal(["saplogon"], desktop.Opened);
        Assert.Equal(["a"], desktop.Typed);
    }

    [Fact]
    public void OpenStep_ThatCannotBeOpened_Fails()
    {
        var desktop = new FakeDesktop { CanOpen = false };

        PlaybackResult result = Play(desktop, [new OpenStep { Path = @"C:\yok.xlsx" }], new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.OpenFailed, 1), (result.Outcome, result.Failure, result.FailedStepNumber));
    }

    [Fact]
    public void KeyStep_InAWindowOfAnotherSize_LeavesTheWindowAlone()
    {
        var desktop = new FakeDesktop();
        desktop.Windows.Add(SapWindow with { Bounds = new ScreenRect(300, 200, 900, 700) });
        MacroStep[] steps = [new KeyPressStep { VirtualKey = VirtualKeys.Return, Target = SapTarget }];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Empty(desktop.Moves);
    }

    [Fact]
    public void InputRefusedByWindows_FailsAtThatStep_InsteadOfGoingOn()
    {
        var desktop = Desktop("Kod001", "Kod002");

        // The screen locks right after Ctrl+C (two key presses).
        desktop.KeysUntilInputBlocked = 2;

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions());

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.InputBlocked), (result.Outcome, result.Failure));
        Assert.Equal(3, result.FailedStepNumber);
        Assert.Empty(desktop.Pasted);
        Assert.Equal(1, desktop.ReleaseAllCalls);
    }

    [Fact]
    public void MissingWindow_FailsAfterTheWaitTime_AndNamesTheWindow()
    {
        var desktop = Desktop("Kod001");
        desktop.Windows.Remove(SapWindow);
        var options = new PlaybackOptions { WindowWait = TimeSpan.FromSeconds(3) };

        PlaybackResult result = Play(desktop, CopyPasteNextRow, options);

        Assert.Equal(PlaybackOutcome.Failed, result.Outcome);
        Assert.Equal(PlaybackFailure.WindowNotFound, result.Failure);
        Assert.Equal(2, result.FailedStepNumber);
        Assert.Equal("SAP Easy Access", result.Window?.Title);
        Assert.InRange(desktop.NowMs, 3000, 3400);
    }

    [Fact]
    public void WindowOfAnAdministratorProgram_FailsClearly()
    {
        var desktop = Desktop("x");
        desktop.ElevatedWindows.Add(SapWindow.Handle);

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions());

        Assert.Equal(PlaybackFailure.TargetIsElevated, result.Failure);
    }

    [Fact]
    public void CopyThatDoesNotChangeTheClipboard_Fails()
    {
        var desktop = Desktop("x");
        desktop.CopyChangesClipboard = false;

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions());

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.ClipboardDidNotChange), (result.Outcome, result.Failure));
    }

    [Fact]
    public void UntilClipboardEmpty_CopyOfAnEmptySelectionAfterSomeRounds_EndsTheList()
    {
        var desktop = Desktop("elma", "armut", "kiraz");
        desktop.CopyPastTheListChangesClipboard = false;

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions());

        Assert.Equal((PlaybackOutcome.ListEnded, 3), (result.Outcome, result.CompletedRounds));
        Assert.Equal(["elma", "armut", "kiraz"], desktop.Pasted);
    }

    [Fact]
    public void Count_CopyThatStopsChangingTheClipboard_StillFails()
    {
        var desktop = Desktop("elma");
        desktop.CopyPastTheListChangesClipboard = false;

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions { Loop = LoopSettings.Times(3) });

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.ClipboardDidNotChange), (result.Outcome, result.Failure));
    }

    [Fact]
    public void PasteWithCleanTextOff_KeepsTheLineBreak()
    {
        var desktop = Desktop("Kod001\r\n");
        MacroStep[] steps = [new CopyStep { Target = ExcelTarget }, new PasteStep { Target = SapTarget, CleanText = false }];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(["Kod001\r\n"], desktop.Pasted);
    }

    [Fact]
    public void DisabledSteps_AreSkipped()
    {
        var desktop = Desktop();
        MacroStep[] steps =
        [
            new TypeTextStep { Text = "a", Target = SapTarget },
            new TypeTextStep { Text = "b", Target = SapTarget, IsEnabled = false },
        ];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(["a"], desktop.Typed);
    }

    [Fact]
    public void Stopping_ReleasesHeldKeys_AndReportsTheFinishedRounds()
    {
        var desktop = Desktop("1", "2", "3", "4", "5", "6");
        using var cancellation = new CancellationTokenSource();
        desktop.OnWait = now =>
        {
            if (desktop.Pasted.Count == 2 && desktop.HeldKeys.Count > 0)
            {
                cancellation.Cancel();
            }
        };

        PlaybackResult result = new MacroPlayer(desktop, desktop, desktop, desktop)
            .Play(Macro(CopyPasteNextRow), new PlaybackOptions(), cancellation.Token);

        Assert.Equal(PlaybackOutcome.Stopped, result.Outcome);
        Assert.Equal(1, result.CompletedRounds);
        Assert.Empty(desktop.HeldKeys);
        Assert.Equal(1, desktop.ReleaseAllCalls);
    }

    [Fact]
    public void PasswordStep_PausesUntilResumed()
    {
        var desktop = Desktop();
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);
        int? passwordStep = null;
        player.PasswordNeeded += (_, step) =>
        {
            passwordStep = step;
            Assert.True(player.IsPaused);
            player.Resume();
        };
        MacroStep[] steps = [new WaitStep { IsPasswordEntry = true }, new TypeTextStep { Text = "x", Target = SapTarget }];

        PlaybackResult result = player.Play(Macro(steps), new PlaybackOptions { Loop = LoopSettings.Times(1) }, CancellationToken.None);

        Assert.Equal(1, passwordStep);
        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal(["x"], desktop.Typed);
    }

    [Fact]
    public void FirstStep_SkipsTheEarlierStepsOnlyInTheFirstRound()
    {
        var desktop = Desktop();
        MacroStep[] steps = [.. "abc".Select(c => new TypeTextStep { Text = c.ToString(), Target = SapTarget })];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(2), FirstStep = 2 });

        Assert.Equal(["b", "c", "a", "b", "c"], desktop.Typed);
    }

    [Fact]
    public void FirstAndLastStep_PlayASingleStep()
    {
        var desktop = Desktop();
        MacroStep[] steps = [.. "abc".Select(c => new TypeTextStep { Text = c.ToString(), Target = SapTarget })];

        Play(desktop, steps, new PlaybackOptions { Loop = LoopSettings.Times(1), FirstStep = 2, LastStep = 2 });

        Assert.Equal(["b"], desktop.Typed);
    }

    [Fact]
    public void Progress_ReportsRoundAndStep()
    {
        var desktop = Desktop("1", "2");
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);
        var seen = new List<PlaybackProgress>();
        player.ProgressChanged += (_, progress) => seen.Add(progress);

        player.Play(Macro(CopyPasteNextRow), new PlaybackOptions { Loop = LoopSettings.Times(2) }, CancellationToken.None);

        Assert.Equal(new PlaybackProgress(1, 2, 1, 6), seen[0]);
        Assert.Equal(new PlaybackProgress(2, 2, 6, 6), seen[^1]);
    }

    private static FakeDesktop Desktop(params string[] cells)
    {
        var desktop = new FakeDesktop(cells);
        desktop.Windows.Add(ExcelWindow);
        desktop.Windows.Add(SapWindow);
        return desktop;
    }

    private static MacroDefinition Macro(IReadOnlyList<MacroStep> steps) => new() { Name = "Test", Steps = steps };

    private static PlaybackResult Play(FakeDesktop desktop, IReadOnlyList<MacroStep> steps, PlaybackOptions options) =>
        new MacroPlayer(desktop, desktop, desktop, desktop).Play(Macro(steps), options, CancellationToken.None);
}
