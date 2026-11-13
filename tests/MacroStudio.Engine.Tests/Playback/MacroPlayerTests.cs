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
    private static readonly WindowTarget ChromeTarget = new("chrome", "(1) WhatsApp - Google Chrome", "Chrome_WidgetWin_1", new ScreenRect(0, 0, 1000, 800));
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

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void ExcelDemo_PastesFiftyRowsInOrderAndStopsAtTheEmptyCell(double speed)
    {
        string[] values = Enumerable.Range(1, 50).Select(row => $"DEMO-{row:000}").ToArray();
        var desktop = Desktop(values.Select(value => value + "\r\n").ToArray());

        PlaybackResult result = Play(desktop, CopyPasteNextRow, new PlaybackOptions { Speed = speed });

        Assert.Equal((PlaybackOutcome.ListEnded, 50), (result.Outcome, result.CompletedRounds));
        Assert.Equal(values, desktop.Pasted);
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
    public void ListRows_StartAtTheChosenRow_AndSaveTheNextRowAfterEachRound()
    {
        var desktop = Desktop();
        var progress = new List<int>();
        var options = new PlaybackOptions
        {
            ListRows = [["a"], ["b"], ["c"], ["d"]],
            ListStartRow = 3,
            ReportNextListRow = progress.Add,
        };

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "{1}" }], options);

        Assert.Equal((PlaybackOutcome.ListEnded, 2), (result.Outcome, result.CompletedRounds));
        Assert.Equal(["c", "d"], desktop.Typed);
        Assert.Equal([3, 4, 5], progress);
    }

    [Fact]
    public void StoppingDuringAListRow_LeavesThatRowAsTheNextRow()
    {
        var desktop = Desktop();
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);
        using var cancel = new CancellationTokenSource();
        var progress = new List<int>();
        player.ProgressChanged += (_, step) =>
        {
            if (step.Round == 2)
            {
                cancel.Cancel();
            }
        };

        PlaybackResult result = player.Play(Macro([new TypeTextStep { Text = "{1}" }]), new PlaybackOptions
        {
            ListRows = [["a"], ["b"], ["c"], ["d"]],
            ListStartRow = 2,
            ReportNextListRow = progress.Add,
        }, cancel.Token);

        Assert.Equal((PlaybackOutcome.Stopped, 1), (result.Outcome, result.CompletedRounds));
        Assert.Equal(["b"], desktop.Typed);
        Assert.Equal([2, 3], progress);
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
    public void ExcelMacroStep_FinishesBeforeTheFollowingStep()
    {
        var desktop = new FakeDesktop();
        const string path = @"C:\Reports\Book.xlsm";
        desktop.ExcelMacro = (workbook, procedure) =>
        {
            Assert.Equal((path, "Module1.UpdateCells"), (workbook, procedure));
            Assert.Equal("before", string.Concat(desktop.Typed));
            desktop.Typed.Add("|Excel finished|");
            return null;
        };

        PlaybackResult result = Play(desktop, [
            new TypeTextStep { Text = "before" },
            new ExcelMacroStep { WorkbookPath = path, Procedure = "Module1.UpdateCells" },
            new TypeTextStep { Text = "after" },
        ], new PlaybackOptions { Loop = LoopSettings.Times(1) });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal("before|Excel finished|after", string.Concat(desktop.Typed));
    }

    [Theory]
    [InlineData(PlaybackFailure.ExcelWorkbookNotOpen, 2)]
    [InlineData(PlaybackFailure.ExcelMacroFailed, 1)]
    public void ExcelMacroStep_DoesNotRetryVbaThatMayAlreadyHaveChangedTheWorkbook(PlaybackFailure failure, int expectedCalls)
    {
        int calls = 0;
        var desktop = new FakeDesktop { ExcelMacro = (_, _) => { calls++; return failure; } };
        PlaybackResult result = Play(desktop, [
            new ExcelMacroStep { WorkbookPath = @"C:\Reports\Book.xlsm", Procedure = "UpdateCells" },
            new TypeTextStep { Text = "after" },
        ], new PlaybackOptions { Loop = LoopSettings.Times(1), OnError = new ErrorPolicy(ErrorAction.Stop, 1) });

        Assert.Equal((PlaybackOutcome.Failed, failure, 1), (result.Outcome, result.Failure, result.FailedStepNumber));
        Assert.Equal(expectedCalls, calls);
        Assert.Empty(desktop.Typed);
    }

    [Fact]
    public void ExcelMacroStep_StopWhileExcelRunsDoesNotPlayTheNextStep()
    {
        using var cancellation = new CancellationTokenSource();
        var desktop = new FakeDesktop { ExcelMacro = (_, _) => { cancellation.Cancel(); return null; } };
        MacroStep[] steps = [
            new ExcelMacroStep { WorkbookPath = @"C:\Reports\Book.xlsm", Procedure = "UpdateCells" },
            new TypeTextStep { Text = "after" },
        ];
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);

        PlaybackResult result = player.Play(new MacroDefinition { Name = "Test", Steps = steps },
            new PlaybackOptions { Loop = LoopSettings.Times(1) }, cancellation.Token);

        Assert.Equal(PlaybackOutcome.Stopped, result.Outcome);
        Assert.Empty(desktop.Typed);
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

    [Theory]
    [InlineData("Liste2.xlsx - Excel")]
    [InlineData("Liste.xlsx - Excel")]
    public void ExcelTargetMustBeUnique_BeforeSendingInput(string otherTitle)
    {
        var desktop = Desktop();
        desktop.Windows.Add(ExcelWindow with { Handle = 3, Title = otherTitle });
        if (otherTitle != ExcelWindow.Title)
        {
            desktop.Windows.Remove(ExcelWindow);
        }

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "değer", Target = ExcelTarget }],
            new PlaybackOptions { WindowWait = TimeSpan.Zero });

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.WindowNotFound), (result.Outcome, result.Failure));
        Assert.Equal(ExcelTarget, result.Window);
        Assert.Empty(desktop.Activations);
        Assert.Empty(desktop.Typed);
    }

    [Fact]
    public void WaitForExcelWorkbookToClose_WaitsWhileMatchingWindowsAreOpen()
    {
        var desktop = Desktop();
        WindowCandidate secondWindow = ExcelWindow with { Handle = 3 };
        desktop.Windows.Add(secondWindow);
        desktop.OnWait = now =>
        {
            if (now >= 200)
            {
                desktop.Windows.Remove(ExcelWindow);
                desktop.Windows.Remove(secondWindow);
            }
        };
        MacroStep[] steps = [new WaitForStep { Condition = WaitCondition.WindowCloses, Target = ExcelTarget, TimeoutMs = 1000 }];

        PlaybackResult result = Play(desktop, steps, new PlaybackOptions());

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.InRange(desktop.NowMs, 200, 400);
    }

    [Theory]
    [InlineData("chrome", " - Google Chrome")]
    [InlineData("msedge", " - Microsoft Edge")]
    [InlineData("msedge", " ve diğer 1 sayfa - Profil 1 - Microsoft\u200B Edge")]
    [InlineData("msedge", " and 1 more page - Profile 1 - Microsoft\u200B Edge")]
    public void BrowserPlayback_SwitchesToTheRecordedTabBeforeTyping(string processName, string windowSuffix)
    {
        var desktop = Desktop();
        WindowTarget target = ChromeTarget with { ProcessName = processName, Title = "(1) WhatsApp" + windowSuffix };
        desktop.Windows.Add(new WindowCandidate(3, processName, target.ClassName, "YouTube" + windowSuffix, target.Bounds));
        desktop.BrowserTabs[3] = ["YouTube", "(7) WhatsApp"];
        desktop.BrowserHosts[3] = "web.whatsapp.com";
        desktop.TryActivate(3);

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "a", Target = target }],
            new PlaybackOptions { WindowWait = TimeSpan.Zero });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Equal((3, "(7) WhatsApp"), Assert.Single(desktop.TabSelections));
        Assert.Equal(["a"], desktop.Typed);
    }

    [Fact]
    public void EdgePlayback_FindsTheRecordedTabAfterOtherTabsAreClosed()
    {
        var desktop = Desktop();
        var target = ChromeTarget with { ProcessName = "msedge", Title = "(1) WhatsApp ve diğer 1 sayfa - Profil 1 - Microsoft\u200B Edge" };
        desktop.Windows.Add(new WindowCandidate(3, "msedge", target.ClassName, "WhatsApp - Profil 1 - Microsoft\u200B Edge", target.Bounds));
        desktop.BrowserTabs[3] = ["WhatsApp"];
        desktop.BrowserHosts[3] = "web.whatsapp.com";

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "a", Target = target }],
            new PlaybackOptions { WindowWait = TimeSpan.Zero });

        Assert.Equal(PlaybackOutcome.Completed, result.Outcome);
        Assert.Empty(desktop.TabSelections);
        Assert.Equal(["a"], desktop.Typed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrAmbiguousBrowserTab_ReportsFailureWithoutSendingInput(bool duplicate)
    {
        var desktop = Desktop();
        desktop.Windows.Add(new WindowCandidate(3, "chrome", ChromeTarget.ClassName, "YouTube - Google Chrome", ChromeTarget.Bounds));
        desktop.BrowserTabs[3] = duplicate ? ["YouTube", "WhatsApp", "(7) WhatsApp"] : ["YouTube", "WhatsApp help"];
        var options = new PlaybackOptions { WindowWait = TimeSpan.Zero, OnError = new ErrorPolicy(ErrorAction.SkipStep, 0) };

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "a", Target = ChromeTarget }], options);

        Assert.Equal((PlaybackOutcome.Failed, PlaybackFailure.BrowserTabNotFound), (result.Outcome, result.Failure));
        Assert.Equal(ChromeTarget, result.Window);
        Assert.Empty(desktop.Activations);
        Assert.Empty(desktop.TabSelections);
        Assert.Empty(desktop.Typed);
    }

    [Fact]
    public void BrowserPlayback_WaitsForTheSelectedTabTitleBeforeSendingInput()
    {
        var desktop = Desktop();
        desktop.Windows.Add(new WindowCandidate(3, "chrome", ChromeTarget.ClassName, "YouTube - Google Chrome", ChromeTarget.Bounds));
        desktop.BrowserTabs[3] = ["YouTube", "WhatsApp"];
        desktop.BrowserSelectionChangesTitle = false;

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "a", Target = ChromeTarget }],
            new PlaybackOptions { WindowWait = TimeSpan.Zero });

        Assert.Equal(PlaybackFailure.BrowserTabNotFound, result.Failure);
        Assert.Single(desktop.TabSelections);
        Assert.Empty(desktop.Typed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("youtube.com")]
    [InlineData("web.whatsapp.com.example")]
    public void BrowserPlayback_DoesNotTypeWhenTheAddressCannotBeVerified(string? activeHost)
    {
        var desktop = Desktop();
        var target = ChromeTarget with { BrowserHost = "web.whatsapp.com" };
        desktop.Windows.Add(new WindowCandidate(3, "chrome", target.ClassName, "YouTube - Google Chrome", target.Bounds));
        desktop.BrowserTabs[3] = ["YouTube", "WhatsApp"];
        if (activeHost is not null)
        {
            desktop.BrowserHosts[3] = activeHost;
        }

        PlaybackResult result = Play(desktop, [new TypeTextStep { Text = "a", Target = target }],
            new PlaybackOptions { WindowWait = TimeSpan.Zero, OnError = new ErrorPolicy(ErrorAction.SkipStep, 0) });

        Assert.Equal(PlaybackFailure.BrowserHostNotVerified, result.Failure);
        Assert.Empty(desktop.Typed);
    }

    [Fact]
    public void BeforePlayback_OnlyReportsMissingTargetsInTheSelectedRangeWithoutActivatingWindows()
    {
        var desktop = Desktop();
        desktop.Windows.Remove(ExcelWindow);
        var macro = Macro([
            new TypeTextStep { Text = "a", Target = ExcelTarget },
            new TypeTextStep { Text = "b", Target = ChromeTarget },
            new FocusWindowStep { Target = ChromeTarget with { Bounds = new ScreenRect(10, 20, 1000, 800) } },
            new FocusWindowStep { Target = ExcelTarget, IsEnabled = false },
            new WaitForStep { Condition = WaitCondition.WindowAppears, Target = ExcelTarget },
        ]);
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);

        Assert.Equal([ChromeTarget], player.FindUnavailableTargets(macro, 2, null, CancellationToken.None));
        Assert.Empty(desktop.Activations);
        Assert.Empty(desktop.TabSelections);
        Assert.Empty(desktop.Typed);
    }

    [Fact]
    public void BeforePlayback_RecognizesAnOpenBackgroundTabWithoutSelectingIt()
    {
        var desktop = Desktop();
        desktop.Windows.Add(new WindowCandidate(3, "chrome", ChromeTarget.ClassName, "YouTube - Google Chrome", ChromeTarget.Bounds));
        desktop.BrowserTabs[3] = ["YouTube", "WhatsApp"];
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);

        Assert.Empty(player.FindUnavailableTargets(Macro([new FocusWindowStep { Target = ChromeTarget }]), 1, null, CancellationToken.None));
        Assert.Empty(desktop.Activations);
        Assert.Empty(desktop.TabSelections);
    }

    [Fact]
    public void FailedPlayback_IdentifiesTheRoundAndTheStepById()
    {
        var desktop = Desktop();
        var first = new TypeTextStep { Text = "a", Target = ExcelTarget };
        var player = new MacroPlayer(desktop, desktop, desktop, desktop);
        player.ProgressChanged += (_, progress) =>
        {
            if (progress.Round == 2)
            {
                desktop.Windows.Remove(ExcelWindow);
            }
        };

        PlaybackResult result = player.Play(Macro([first]),
            new PlaybackOptions { Loop = LoopSettings.Times(2), WindowWait = TimeSpan.Zero }, CancellationToken.None);

        Assert.Equal((PlaybackOutcome.Failed, 2, 1, first.Id), (result.Outcome, result.FailedRound, result.FailedStepNumber, result.FailedStepId));
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
