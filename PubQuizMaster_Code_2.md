<!--
INSTRUCTIONS FOR AI:
- This is chunk 2 of 2 of the 'PubQuizMaster' codebase export.
- Do NOT start any analysis, summary, or response until ALL 2 chunks have been provided.
- After each chunk except the last, simply confirm receipt (e.g. "Chunk 2 of 2 received. Please continue.").
- Begin your analysis only after the user explicitly confirms that all chunks have been uploaded.
-->

## FILE: Desktop\Views\MainWindow.axaml

```axaml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:PubQuizMaster.Desktop.ViewModels"
        xmlns:views="using:PubQuizMaster.Desktop.Views"
        x:Class="PubQuizMaster.Desktop.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="PubQuizMaster – Host"
        Width="1280" Height="800"
        Background="{StaticResource BrushBg}">

    <Grid RowDefinitions="Auto,*">

        <!-- Header -->
        <Border Grid.Row="0" Background="{StaticResource BrushSurface}" Padding="16 12">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Orientation="Horizontal" Spacing="16">
                    <TextBlock Text="🎯 PubQuizMaster" FontSize="18" FontWeight="Bold"
                               Foreground="{StaticResource BrushAccent}"/>
                    <TextBlock Text="{Binding QuizNightName}" FontSize="14"
                               Foreground="{StaticResource BrushSubtext}" VerticalAlignment="Center"/>
                </StackPanel>
                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8">
                    <TextBlock Text="🌐" VerticalAlignment="Center"/>
                    <Button Grid.Column="1"
                            Content="{Binding ServerUrl}"
                            Command="{Binding OpenInBrowserCommand}"
                            FontSize="12"
                            Foreground="{StaticResource BrushSubtext}"
                            VerticalAlignment="Center"
                            Background="Transparent"
                            Padding="0"
                            Cursor="Hand"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Body -->
        <Grid Grid.Row="1" RowDefinitions="*,Auto" Margin="16">

            <!-- Scoring — volle Breite -->
            <Grid Grid.Row="0" IsVisible="{Binding IsScoring}">
                <views:ActiveRoundView DataContext="{Binding ActiveRound}"/>
            </Grid>

            <!-- Review — linke + mittlere Spalte -->
            <Grid Grid.Row="0" IsVisible="{Binding IsReview}"
                  ColumnDefinitions="1*,4,3*">

                <Border Grid.Column="0"
                        Background="{StaticResource BrushSurface}" CornerRadius="8"
                        Margin="0 0 0 0" Padding="12">
                    <views:LeftPanelView DataContext="{Binding LeftPanel}"/>
                </Border>

                <GridSplitter Grid.Column="1"
                              ResizeDirection="Columns"
                              Background="{StaticResource BrushCard}"
                              HorizontalAlignment="Stretch"
                              VerticalAlignment="Stretch"/>

                <Grid Grid.Column="2" RowDefinitions="Auto,*" Margin="0 0 0 0">
                    <Border Grid.Row="0"
                            Background="#3a1a1a" CornerRadius="8"
                            Padding="12 8" Margin="0 0 0 8"
                            IsVisible="{Binding Setup.SetupError,
                            Converter={x:Static StringConverters.IsNotNullOrEmpty}}">
                        <TextBlock Text="{Binding Setup.SetupError}"
                                   Foreground="{StaticResource BrushWrong}" FontSize="13"/>
                    </Border>
                    <ContentControl Grid.Row="1" Content="{Binding Center.CurrentContent}">
                        <ContentControl.DataTemplates>
                            <DataTemplate DataType="vm:SetupViewModel">
                                <views:SetupView/>
                            </DataTemplate>
                            <DataTemplate DataType="vm:RoundMatrixViewModel">
                                <views:RoundMatrixView/>
                            </DataTemplate>
                        </ContentControl.DataTemplates>
                    </ContentControl>
                </Grid>

            </Grid>

            <!-- Einziger Button — volle Breite, immer unten -->
            <Button Grid.Row="1"
                    IsVisible="{Binding ShowStartButton}"
                    Content="▶  Start Round"
                    Classes="primary"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Center"
                    FontSize="16" Padding="0 14"
                    Margin="0 12 0 0"
                    IsEnabled="{Binding Setup.CanStartRound}"
                    Command="{Binding StartRoundCommand}"/>

            <Button Grid.Row="1"
                    IsVisible="{Binding IsScoring}"
                    Content="🏁  Finalize Round"
                    Classes="primary"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Center"
                    FontSize="16" Padding="0 14"
                    Margin="0 12 0 0"
                    Command="{Binding FinalizeRoundCommand}"/>

            <Button Grid.Row="1"
                    IsVisible="{Binding ShowNewRoundButton}"
                    Content="+ New Round"
                    Classes="primary"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Center"
                    FontSize="16" Padding="0 14"
                    Margin="0 12 0 0"
                    Command="{Binding LeftPanel.NewRoundCommand}"/>

        </Grid>


    </Grid>
</Window>

```


## FILE: Desktop\Views\MainWindow.axaml.cs

```cs
using Avalonia.Controls;

namespace PubQuizMaster.Desktop.Views
{
    public partial class MainWindow 
        : Window
    {
        #region Public Constructors

        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion Public Constructors
    }
}
```


## FILE: Desktop\Views\RoundMatrixView.axaml

```axaml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PubQuizMaster.Desktop.ViewModels"
             x:Class="PubQuizMaster.Desktop.Views.RoundMatrixView"
             x:DataType="vm:RoundMatrixViewModel">

    <DockPanel>

        <!-- Toolbar: Edit / Save / Cancel / Export -->
        <Border DockPanel.Dock="Top" Background="{StaticResource BrushSurface}"
                CornerRadius="8" Padding="12 10" Margin="0 0 0 12">
            <Grid ColumnDefinitions="*,Auto">
                <TextBlock Grid.Column="0"
                           Text="{Binding RoundName}"
                           FontSize="16" FontWeight="Bold"
                           VerticalAlignment="Center"/>

                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8">

                    <!-- Edit-Modus aus -->

                    <Button Content="✎ Edit"
                            IsVisible="{Binding !IsEditing}"
                            Command="{Binding EditCommand}"/>

                    <!-- Edit-Modus an -->

                    <Button Content="✓ Save" Classes="primary"
                            IsVisible="{Binding IsEditing}"
                            Command="{Binding SaveCommand}"/>
                    <Button Content="✕ Cancel" Classes="danger"
                            IsVisible="{Binding IsEditing}"
                            Command="{Binding CancelCommand}"/>

                    <Button Content="🗑 Delete"
                            Classes="danger"
                            Padding="8 6"
                            FontSize="13"
                            IsVisible="{Binding IsEditing}"
                            Command="{Binding DeleteRoundCommand}"/>

                    <!-- Export immer sichtbar -->
                    <Button Content="↗ Export"
                            Command="{Binding ExportCommand}"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Matrix -->
        <ScrollViewer HorizontalScrollBarVisibility="Auto"
                      VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="4 0 0 0">

                <!-- Header-Zeile -->
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="Team" Width="140"
                               FontSize="11" Foreground="{StaticResource BrushSubtext}"/>
                    <ItemsControl ItemsSource="{Binding QuestionHeaders}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <StackPanel Orientation="Horizontal"/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding}" Width="32"
                                           FontSize="11" Foreground="{StaticResource BrushSubtext}"
                                           TextAlignment="Center"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    <TextBlock Text="Σ" Width="32"
                               FontSize="11" Foreground="{StaticResource BrushSubtext}"
                               TextAlignment="Center"/>
                </StackPanel>

                <!-- Team-Zeilen -->
                <ItemsControl ItemsSource="{Binding Rows}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate x:DataType="vm:TeamAnswerRowViewModel">
                            <StackPanel Orientation="Horizontal" Margin="0 4">
                                <TextBlock Text="{Binding TeamName}"
                                           Width="140" FontSize="13"
                                           TextTrimming="CharacterEllipsis"
                                           VerticalAlignment="Center"/>
                                <ItemsControl ItemsSource="{Binding Answers}">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <StackPanel Orientation="Horizontal"/>
                                        </ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate x:DataType="vm:AnswerCellViewModel">
                                            <Panel Width="32" HorizontalAlignment="Center">
                                                <TextBlock Text="{Binding Display}"
                                                           Foreground="{Binding Color}"
                                                           FontSize="14"
                                                           TextAlignment="Center"
                                                           VerticalAlignment="Center"
                                                           IsVisible="{Binding !IsEditing}"/>
                                                <CheckBox IsChecked="{Binding IsCorrect}"
                                                          IsThreeState="True"
                                                          IsVisible="{Binding IsEditing}"
                                                          HorizontalAlignment="Center"
                                                          VerticalAlignment="Center"/>
                                            </Panel>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                                <TextBlock Text="{Binding Total}"
                                           Width="32" FontSize="13"
                                           Foreground="{StaticResource BrushAccent}" FontWeight="Bold"
                                           TextAlignment="Center"
                                           VerticalAlignment="Center"/>
                            </StackPanel>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <!-- Summenzeile -->
                <Border Height="1" Background="{StaticResource BrushCard}" Margin="0 6"/>
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="Correct" Width="140"
                               FontSize="11" Foreground="{StaticResource BrushSubtext}"
                               VerticalAlignment="Center"/>
                    <ItemsControl ItemsSource="{Binding ColSums}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <StackPanel Orientation="Horizontal"/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding}" Width="32"
                                           FontSize="13" Foreground="{StaticResource BrushCorrect}"
                                           FontWeight="Bold" TextAlignment="Center"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>

            </StackPanel>
        </ScrollViewer>

    </DockPanel>
</UserControl>

```


## FILE: Desktop\Views\RoundMatrixView.axaml.cs

```cs
using Avalonia.Controls;

namespace PubQuizMaster.Desktop.Views
{
    public partial class RoundMatrixView : UserControl
    {
        #region Public Constructors

        public RoundMatrixView()
        {
            InitializeComponent();
        }

        #endregion Public Constructors
    }
}
```


## FILE: Desktop\Views\SetupView.axaml

```axaml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PubQuizMaster.Desktop.ViewModels"
             x:Class="PubQuizMaster.Desktop.Views.SetupView"
             x:DataType="vm:SetupViewModel">

    <Grid ColumnDefinitions="*,4,*">

        <!-- Teams -->
        <Border Grid.Column="0" Background="{StaticResource BrushSurface}" CornerRadius="8" Padding="16">
            <DockPanel>
                <TextBlock DockPanel.Dock="Top" Text="Teams"
                           FontSize="14" FontWeight="Bold" Margin="0 0 0 10"/>

                <Grid DockPanel.Dock="Top" ColumnDefinitions="*,Auto"
                      ColumnSpacing="6" Margin="0 0 0 8">
                    <TextBox Grid.Column="0" Text="{Binding NewTeamName}"
                             Watermark="Team name…"/>
                    <Button Grid.Column="1" Content="Add"
                            Command="{Binding AddTeamCommand}"/>
                </Grid>

                <ScrollViewer>
                    <ItemsControl ItemsSource="{Binding Teams}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="vm:TeamViewModel">
                                <Grid ColumnDefinitions="*,Auto" Margin="0 3">
                                    <TextBlock Grid.Column="0" Text="{Binding Name}"
                                               VerticalAlignment="Center"/>
                                    <Button Grid.Column="1" Content="✕"
                                            Classes="danger" Padding="6 4"
                                            Command="{Binding $parent[UserControl].DataContext.RemoveTeamCommand}"
                                            CommandParameter="{Binding}"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>
            </DockPanel>
        </Border>

        <GridSplitter Grid.Column="1"
                      ResizeDirection="Columns"
                      Background="{StaticResource BrushCard}"
                      HorizontalAlignment="Stretch"
                      VerticalAlignment="Stretch"/>

        <!-- Round Config + Scorer Assignments -->
        <DockPanel Grid.Column="2" LastChildFill="True">

            <Border DockPanel.Dock="Top" Background="{StaticResource BrushSurface}"
                    CornerRadius="8" Padding="16" Margin="8 0 0 10">
                <StackPanel Spacing="10">
                    <TextBlock Text="Round" FontSize="14" FontWeight="Bold"/>
                    <Grid ColumnDefinitions="90,*" RowDefinitions="Auto,Auto"
                          RowSpacing="8" ColumnSpacing="12">
                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Name"
                                   Foreground="{StaticResource BrushSubtext}" VerticalAlignment="Center"/>
                        <TextBox   Grid.Row="0" Grid.Column="1"
                                 Text="{Binding RoundName}"/>
                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Questions"
                                   Foreground="{StaticResource BrushSubtext}" VerticalAlignment="Center"/>
                        <NumericUpDown Grid.Row="1" Grid.Column="1"
                                       Value="{Binding QuestionCount}"
                                       Minimum="1" Maximum="50"
                                       FormatString="0"
                                       Increment="1"
                                       AllowSpin="True"/>

                    </Grid>
                </StackPanel>
            </Border>

            <!-- Scorer assignments -->
            <Border Background="{StaticResource BrushSurface}"
                    CornerRadius="8" Padding="12" Margin="8 0 0 0">
                <DockPanel LastChildFill="True">
                    <Grid DockPanel.Dock="Top" ColumnDefinitions="*,Auto" Margin="0 0 0 8">
                        <TextBlock Text="Scorer Assignments"
                                   FontSize="14" FontWeight="Bold"/>
                        <Button Grid.Column="1" Content="+ Scorer"
                                Padding="8 4"
                                Command="{Binding AddScorerCommand}"/>
                    </Grid>
                    <ScrollViewer>
                        <ItemsControl ItemsSource="{Binding Assignments}">                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="vm:ScorerAssignmentViewModel">
                                    <Border Background="{StaticResource BrushCard}" CornerRadius="6"
                                            Padding="10 8" Margin="0 3">
                                        <StackPanel Spacing="6">
                                            <Grid ColumnDefinitions="*,Auto">
                                                <TextBox Grid.Column="0"
                                                         Text="{Binding Label}"
                                                         Watermark="Label…" Padding="6 4"/>
                                                <Button Grid.Column="1" Content="✕"
                                                        Classes="danger" Padding="6 4"
                                                        Margin="6 0 0 0"
                                                        Command="{Binding $parent[UserControl].DataContext.RemoveScorerCommand}"
                                                        CommandParameter="{Binding}"/>
                                            </Grid>
                                            <ItemsControl ItemsSource="{Binding Teams}">
                                                <ItemsControl.ItemsPanel>
                                                    <ItemsPanelTemplate>
                                                        <WrapPanel Orientation="Horizontal"/>
                                                    </ItemsPanelTemplate>
                                                </ItemsControl.ItemsPanel>
                                                <ItemsControl.ItemTemplate>
                                                    <DataTemplate x:DataType="vm:AssignableTeamViewModel">
                                                        <CheckBox Content="{Binding Name}"
                                                                  IsChecked="{Binding IsAssigned}"
                                                                  IsEnabled="{Binding !IsDisabled}"
                                                                  Margin="0 0 10 2"/>
                                                    </DataTemplate>
                                                </ItemsControl.ItemTemplate>
                                            </ItemsControl>
                                        </StackPanel>
                                    </Border>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </ScrollViewer>
                </DockPanel>
            </Border>

        </DockPanel>
    </Grid>
</UserControl>

```


## FILE: Desktop\Views\SetupView.axaml.cs

```cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PubQuizMaster.Desktop.Views;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
    }
}
```


## FILE: Desktop\Views\StartupWindow.axaml

```axaml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:PubQuizMaster.Desktop.ViewModels"
        xmlns:md="using:PubQuizMaster.Desktop.Models"
        x:Class="PubQuizMaster.Desktop.Views.StartupWindow"
        x:DataType="vm:StartupViewModel"
        Title="PubQuizMaster"
        Width="540" Height="780"
        CanResize="False"
        WindowStartupLocation="CenterScreen"
        Background="{StaticResource BrushBg}">

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="32" Spacing="16">

            <!-- Header -->
            <StackPanel Spacing="4" Margin="0 0 0 12">
                <TextBlock Text="🎯 PubQuizMaster"
                           FontSize="28" FontWeight="Bold"
                           Foreground="{StaticResource BrushAccent}"
                           HorizontalAlignment="Center"/>
                <TextBlock Text="Host Setup"
                           FontSize="14"
                           Foreground="{StaticResource BrushSubtext}"
                           HorizontalAlignment="Center"/>
            </StackPanel>

            <!-- ══ CONNECTION CARD ══ -->
            <Border Background="{StaticResource BrushSurface}"
                    CornerRadius="10" Padding="20" Margin="0 0 0 16">
                <StackPanel Spacing="14">

                    <TextBlock Text="🌐 Client Connection"
                               FontSize="14" FontWeight="Bold"/>

                    <!-- Mode Toggle -->
                    <StackPanel Orientation="Horizontal" Spacing="20">
                        <RadioButton Content="💻  Local network"
                                     GroupName="mode"
                                     IsChecked="{Binding IsLocalMode}"/>
                        <RadioButton Content="🌍  Tunnel (external URL)"
                                     GroupName="mode"
                                     IsChecked="{Binding IsTunnelMode, Mode=TwoWay}"/>
                    </StackPanel>

                    <!-- Local: zeig nur die URL -->
                    <Border Background="{StaticResource BrushCard}"
                            CornerRadius="8" Padding="12 10"
                            IsVisible="{Binding IsLocalMode}">
                        <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
                            <Ellipse Grid.Column="0" Width="10" Height="10"
                                     Fill="{StaticResource BrushCorrect}"
                                     VerticalAlignment="Center"/>
                            <TextBlock Grid.Column="1"
                                       Text="{Binding ClientUrl}"
                                       FontSize="13" FontWeight="Bold"
                                       VerticalAlignment="Center"
                                       TextTrimming="CharacterEllipsis"/>
                        </Grid>
                    </Border>

                    <!-- Tunnel: Anleitung + URL-Eingabe -->
                    <StackPanel Spacing="10" IsVisible="{Binding IsTunnelMode}">

                        <!-- Anleitung -->
                        <Border Background="{StaticResource BrushCard}"
                                CornerRadius="8" Padding="12">
                            <StackPanel Spacing="6">
                                <TextBlock Text="How to get a tunnel URL:"
                                           FontSize="12" FontWeight="Bold"/>

                                <TextBlock FontSize="11" Foreground="{StaticResource BrushSubtext}"
                                           TextWrapping="Wrap"
                                           Text="Option A — Pinggy (free, no install):"/>
                                <SelectableTextBlock
                                    FontSize="11" FontFamily="Courier New"
                                    Foreground="{StaticResource BrushAccent}"
                                    TextWrapping="Wrap"
                                    Text="ssh -p 443 -R0:127.0.0.1:5000 a.pinggy.io"/>
                                <TextBlock FontSize="11" Foreground="{StaticResource BrushSubtext}"
                                           TextWrapping="Wrap"
                                           Text="→ Copy the https://… URL from the terminal output."/>

                                <Border Margin="0,4,0,4" MinHeight="1" MaxHeight="1"
                                        Background="{StaticResource BrushSubtext}" />

                                <TextBlock FontSize="11" Foreground="{StaticResource BrushSubtext}"
                                           TextWrapping="Wrap"
                                           Text="Option B — ngrok:"/>
                                <SelectableTextBlock
                                    FontSize="11" FontFamily="Courier New"
                                    Foreground="{StaticResource BrushAccent}"
                                    TextWrapping="Wrap"
                                    Text="ngrok http 5000"/>
                                <TextBlock FontSize="11" Foreground="{StaticResource BrushSubtext}"
                                           TextWrapping="Wrap"
                                           Text="→ Copy the Forwarding URL from the ngrok output."/>
                            </StackPanel>
                        </Border>

                        <!-- URL-Eingabe -->
                        <TextBox Text="{Binding ClientUrl}"
                                 Watermark="Paste tunnel URL here, e.g. https://abc123.a.pinggy.io"
                                 FontSize="12"/>

                        <!-- Status -->
                        <Border Background="{StaticResource BrushCard}"
                                CornerRadius="8" Padding="12 10"
                                IsVisible="{Binding IsConnected}">
                            <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
                                <Ellipse Grid.Column="0" Width="10" Height="10"
                                         Fill="{StaticResource BrushCorrect}"
                                         VerticalAlignment="Center"/>
                                <TextBlock Grid.Column="1"
                                           Text="{Binding ClientUrl}"
                                           FontSize="12" FontWeight="Bold"
                                           VerticalAlignment="Center"
                                           TextTrimming="CharacterEllipsis"/>
                            </Grid>
                        </Border>

                    </StackPanel>

                </StackPanel>
            </Border>


            <!-- Card: New Quiz Night -->
            <Border Background="{StaticResource BrushSurface}"
                    CornerRadius="10" Padding="20">
                <StackPanel Spacing="12">
                    <TextBlock Text="New Quiz Night"
                               FontSize="14" FontWeight="Bold"/>
                    <Grid ColumnDefinitions="*,12,Auto" ColumnSpacing="0">
                        <TextBox Grid.Column="0"
                                 Text="{Binding NewNightName}"
                                 Watermark="Quiz Night name e.g. Pub Quiz – April 2026"/>
                        <CalendarDatePicker Grid.Column="2"
                                            SelectedDate="{Binding NewNightDate}"
                                            HorizontalAlignment="Stretch"/>
                    </Grid>
                    <Button Content="▶  Create &amp; Start"
                            Classes="primary"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            Padding="0 12"
                            Command="{Binding CreateNewCommand}"/>
                </StackPanel>
            </Border>

            <!-- Divider -->
            <Grid ColumnDefinitions="*,Auto,*">
                <Border Grid.Column="0" Height="1"
                        Background="{StaticResource BrushCard}"
                        VerticalAlignment="Center"/>
                <TextBlock Grid.Column="1" Text="or"
                           Foreground="{StaticResource BrushNeutral}"
                           Margin="12 0"/>
                <Border Grid.Column="2" Height="1"
                        Background="{StaticResource BrushCard}"
                        VerticalAlignment="Center"/>
            </Grid>

            <!-- Card: Load Saved Night -->
            <Border Background="{StaticResource BrushSurface}"
                    CornerRadius="10" Padding="20">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top"
                               Text="Load Saved Night"
                               FontSize="14" FontWeight="Bold"
                               Margin="0 0 0 10"/>
                    <TextBlock DockPanel.Dock="Top"
                               Text="{Binding ErrorMessage}"
                               Foreground="{StaticResource BrushWrong}"
                               FontSize="12"
                               IsVisible="{Binding ErrorMessage,
                               Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
                               Margin="0 0 0 8"/>
                    <Button DockPanel.Dock="Bottom"
                            Content="↩  Load Selected"
                            HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center"
                            Padding="0 12" Margin="0 10 0 0"
                            IsEnabled="{Binding SelectedNight,
                            Converter={x:Static ObjectConverters.IsNotNull}}"
                            Command="{Binding LoadCommand}"/>
                    <TextBlock DockPanel.Dock="Top"
                               Text="No saved nights found."
                               Foreground="{StaticResource BrushNeutral}"
                               FontSize="12" HorizontalAlignment="Center"
                               Margin="0 12 0 0"
                               IsVisible="{Binding !SavedNights.Count}"/>
                    <ListBox ItemsSource="{Binding SavedNights}"
                             SelectedItem="{Binding SelectedNight}"
                             IsVisible="{Binding SavedNights.Count}"
                             MaxHeight="160">
                        <ListBox.ItemTemplate>
                            <DataTemplate x:DataType="md:SavedNightEntry">
                                <Grid ColumnDefinitions="*,Auto">
                                    <TextBlock Grid.Column="0" Text="{Binding DisplayName}"
                                               FontSize="13" TextTrimming="CharacterEllipsis"/>
                                    <TextBlock Grid.Column="1" Text="{Binding LastModified}"
                                               Foreground="{StaticResource BrushSubtext}"
                                               FontSize="11" VerticalAlignment="Center"
                                               Margin="8 0 0 0"/>
                                </Grid>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </DockPanel>
            </Border>

            <Border Height="24"/>

        </StackPanel>
    </ScrollViewer>
</Window>

```


## FILE: Desktop\Views\StartupWindow.axaml.cs

```cs
using Avalonia.Controls;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class StartupWindow
        : Window
    {
        #region Public Constructors

        public StartupWindow()
        {
            InitializeComponent();
        }

        public StartupWindow(StartupViewModel vm) 
            : this()
        {
            DataContext = vm;
            vm.CloseRequested = Close;
        }

        #endregion Public Constructors
    }
}
```


## FILE: Desktop\Web\KestrelHost.cs

```cs
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PubQuizMaster.Core.Hub;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.Web
{
    public class KestrelHost(QuizNightService quizNightService, PersistenceService persistenceService,
        ScorerSessionService scorerSessionService)
    {
        #region Public Fields

        public const int Port = 5000;

        #endregion Public Fields

        #region Private Fields

        private WebApplication? _app;
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        #endregion Private Fields

        #region Public Events

        // Fired when the server is ready — carries the local URL for QR code display
        public event Action<string>? ServerReady;

        /// <summary>Fired when the tunnel drops.</summary>
        public event Action? TunnelLost;

        /// <summary>Fired when the Pinggy tunnel URL is available.</summary>
        public event Action<string>? TunnelReady;

        #endregion Public Events

        #region Public Properties

        // Exposes IHubContext so Avalonia ViewModels can push events to clients
        public IHubContext<QuizHub>? HubContext { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public static string GetLocalIpAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(address.Address)) continue;

                    // Skip APIPA (169.254.x.x) — no real network connection
                    if (address.Address.GetAddressBytes()[0] == 169 &&
                        address.Address.GetAddressBytes()[1] == 254) continue;

                    return address.Address.ToString();
                }
            }

            return "localhost";
        }

        /// <summary>
        /// Allows external code (e.g. StartupViewModel) to raise TunnelReady
        /// after a manually managed PinggyTunnel resolves its URL.
        /// </summary>
        public void NotifyTunnelReady(string url) => TunnelReady?.Invoke(url);

        public void Start()
        {
            _cts = new CancellationTokenSource();

            var builder = WebApplication.CreateBuilder();

            // 1. NEU: Host-Filtering deaktivieren (erlaubt Anfragen von xyz.pgy.io)
            builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
            {
                options.AllowedHosts.Add("*");
            });

            // ── Kestrel binding ──────────────────────────────────────────────
            // 0.0.0.0 = all network interfaces, including hotspot adapters.
            // Avoids localhost-only binding which would be invisible to mobile clients.
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(Port);
                // options.Listen(IPAddress.Any, Port);
            });

            // ── Service registration ─────────────────────────────────────────
            // Register the shared singleton instances — the same objects
            // that the Avalonia ViewModels use. No duplication of state.
            builder.Services.AddSingleton(quizNightService);
            builder.Services.AddSingleton(persistenceService);
            builder.Services.AddSingleton(scorerSessionService);

            // SignalR with JSON polymorphism support for AnswerBase
            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    // Required so AnswerBool / AnswerPoint survive serialization
                    options.PayloadSerializerOptions.Converters.Add(new AnswerBaseJsonConverter());
                });

            // CORS — open for local network access (no credentials, no external risk)
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            _app = builder.Build();

            _app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                    Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
            });

            // ── Middleware pipeline ──────────────────────────────────────────
            _app.UseCors();

            // Serve static files from wwwroot (index.html + signalr.min.js)
            _app.UseStaticFiles();

            // Map SignalR hub
            _app.MapHub<QuizHub>("/quizhub");

            // Fallback: any unknown route returns index.html (SPA behavior)
            _app.MapFallbackToFile("index.html");

            // Grab IHubContext so Avalonia can push events to clients
            HubContext = _app.Services.GetRequiredService<IHubContext<QuizHub>>();

            _app.Lifetime.ApplicationStarted.Register(() =>
            {
                var localUrl = $"http://{GetLocalIpAddress()}:{Port}";
                ServerReady?.Invoke(localUrl);
            });

            // ── Run on background thread ─────────────────────────────────────
            _runTask = Task.Run(async () =>
            {
                await _app.RunAsync(_cts.Token);
            });
        }

        public async Task StopAsync()
        {
            if (_cts == null || _app == null) return;

            _cts.Cancel();

            if (_runTask != null)
                await _runTask.ConfigureAwait(false);

            await _app.DisposeAsync();
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\wwwroot\index.html

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no" />
    <title>PubQuizMaster – Scorer</title>
    <style>
        /* ── Reset & Base ─────────────────────────────────────────── */
        *, *::before, *::after {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }

        :root {
            --color-bg: #1a1a2e;
            --color-surface: #16213e;
            --color-card: #0f3460;
            --color-accent: #e94560;
            --color-correct: #2ecc71;
            --color-wrong: #e74c3c;
            --color-neutral: #4a4a6a;
            --color-text: #eaeaea;
            --color-subtext: #9999bb;
            --radius: 12px;
            --tap-min: 64px;
        }

        html, body {
            height: 100%;
            background: var(--color-bg);
            color: var(--color-text);
            font-family: system-ui, -apple-system, sans-serif;
            font-size: 16px;
            overscroll-behavior: none;
        }

        /* ── Layout ───────────────────────────────────────────────── */
        #app {
            display: flex;
            flex-direction: column;
            height: 100dvh;
            max-width: 480px;
            margin: 0 auto;
        }

        /* ── Screens ──────────────────────────────────────────────── */
        .screen {
            display: none;
            flex-direction: column;
            height: 100%;
        }

            .screen.active {
                display: flex;
            }

        /* -- Sort Screen -------------------------------------------- */
        #sort-team-list li {
            background: var(--color-surface);
            border-radius: var(--radius);
            padding: 14px 16px;
            display: flex;
            align-items: center;
            gap: 14px;
            font-size: 1.1rem;
            font-weight: 600;
        }

        .sort-number {
            width: 32px;
            height: 32px;
            border-radius: 50%;
            background: var(--color-accent);
            color: #fff;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 0.95rem;
            font-weight: 700;
            flex-shrink: 0;
        }

        /* ── Connect Screen ───────────────────────────────────────── */
        #screen-connect {
            justify-content: center;
            align-items: center;
            gap: 24px;
            padding: 32px;
        }

            #screen-connect h1 {
                font-size: 1.8rem;
                font-weight: 700;
                color: var(--color-accent);
                text-align: center;
            }

            #screen-connect p {
                color: var(--color-subtext);
                text-align: center;
                font-size: 0.95rem;
            }

        .input-group {
            display: flex;
            flex-direction: column;
            gap: 8px;
            width: 100%;
        }

            .input-group label {
                font-size: 0.85rem;
                color: var(--color-subtext);
                text-transform: uppercase;
                letter-spacing: 0.05em;
            }

            .input-group input {
                background: var(--color-surface);
                border: 1px solid var(--color-neutral);
                border-radius: var(--radius);
                color: var(--color-text);
                font-size: 1.1rem;
                padding: 14px 16px;
                width: 100%;
                outline: none;
                transition: border-color 0.2s;
            }

                .input-group input:focus {
                    border-color: var(--color-accent);
                }

        /* ── Buttons ──────────────────────────────────────────────── */
        .btn {
            border: none;
            border-radius: var(--radius);
            cursor: pointer;
            font-size: 1rem;
            font-weight: 600;
            min-height: var(--tap-min);
            padding: 0 24px;
            transition: opacity 0.15s, transform 0.1s;
            width: 100%;
        }

            .btn:active {
                transform: scale(0.97);
                opacity: 0.85;
            }

        .btn-primary {
            background: var(--color-accent);
            color: #fff;
        }

        .btn-correct {
            background: var(--color-correct);
            color: #fff;
            font-size: 2rem;
            flex: 1;
            border-radius: var(--radius);
            border: none;
            cursor: pointer;
            transition: opacity 0.15s, transform 0.1s;
            min-height: 120px;
        }

        .btn-wrong {
            background: var(--color-wrong);
            color: #fff;
            font-size: 2rem;
            flex: 1;
            border-radius: var(--radius);
            border: none;
            cursor: pointer;
            transition: opacity 0.15s, transform 0.1s;
            min-height: 120px;
        }

            .btn-correct:active, .btn-wrong:active {
                transform: scale(0.97);
                opacity: 0.85;
            }

        .btn-secondary {
            background: var(--color-card);
            color: var(--color-text);
        }

        .btn-sm {
            font-size: 0.85rem;
            min-height: 44px;
            padding: 0 16px;
            width: auto;
        }

        /* ── Status Badge ─────────────────────────────────────────── */
        #connection-status {
            position: fixed;
            top: 12px;
            right: 12px;
            width: 10px;
            height: 10px;
            border-radius: 50%;
            background: var(--color-wrong);
            transition: background 0.3s;
        }

            #connection-status.connected {
                background: var(--color-correct);
            }

            #connection-status.connecting {
                background: #f39c12;
            }

        /* ── Header ───────────────────────────────────────────────── */
        .header {
            background: var(--color-surface);
            border-bottom: 1px solid var(--color-card);
            padding: 12px 16px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-shrink: 0;
        }

        .header-title {
            font-size: 0.9rem;
            font-weight: 600;
            color: var(--color-subtext);
            text-transform: uppercase;
            letter-spacing: 0.05em;
        }

        .header-round {
            font-size: 0.85rem;
            color: var(--color-accent);
            font-weight: 600;
        }

        /* ── Progress Bar ─────────────────────────────────────────── */
        .progress-bar {
            height: 4px;
            background: var(--color-card);
            flex-shrink: 0;
        }

        .progress-fill {
            height: 100%;
            background: var(--color-accent);
            transition: width 0.3s;
        }

        /* ── Scoring Screen ───────────────────────────────────────── */
        #screen-scoring {
            justify-content: space-between;
        }

        .question-info {
            padding: 20px 16px 8px;
            text-align: center;
            flex-shrink: 0;
        }

        .question-label {
            font-size: 0.8rem;
            color: var(--color-subtext);
            text-transform: uppercase;
            letter-spacing: 0.08em;
            margin-bottom: 4px;
        }

        .question-number {
            font-size: 2.5rem;
            font-weight: 700;
            line-height: 1;
        }

        /* ── Team Card ────────────────────────────────────────────── */
        .team-area {
            flex: 1;
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            padding: 16px;
            gap: 12px;
        }

        .team-name {
            font-size: 1.8rem;
            font-weight: 700;
            text-align: center;
            line-height: 1.2;
        }

        .team-counter {
            font-size: 0.85rem;
            color: var(--color-subtext);
        }

        /* ── Answer Buttons ───────────────────────────────────────── */
        .answer-buttons {
            display: flex;
            gap: 12px;
            padding: 0 16px;
            flex-shrink: 0;
        }

        /* ── Navigation ───────────────────────────────────────────── */
        .nav-bar {
            display: flex;
            gap: 8px;
            padding: 12px 16px;
            background: var(--color-surface);
            border-top: 1px solid var(--color-card);
            flex-shrink: 0;
        }

        .nav-bar button:disabled {
            opacity: 0.35;
            cursor: default;
        }

        /* ── Answer Indicator ─────────────────────────────────────── */
        .answer-indicator {
            width: 48px;
            height: 48px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 1.4rem;
            background: var(--color-card);
            transition: background 0.2s;
        }

            .answer-indicator.correct {
                background: var(--color-correct);
            }

            .answer-indicator.wrong {
                background: var(--color-wrong);
            }

        /* ── Overview Screen ──────────────────────────────────────── */
        #screen-overview {
            flex-direction: column;
        }

        .overview-scroll {
            flex: 1;
            overflow-y: auto;
            padding: 12px 16px;
            display: flex;
            flex-direction: column;
            gap: 8px;
        }

        .overview-row {
            background: var(--color-surface);
            border-radius: var(--radius);
            padding: 12px 16px;
            display: grid;
            grid-template-columns: 140px 1fr auto;
            align-items: center;
            gap: 8px;
        }

        .overview-team {
            font-weight: 600;
            font-size: 1rem;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }

        .overview-dots {
            display: flex;
            gap: 4px;
            flex-wrap: nowrap;
        }

        .dot {
            width: 12px;
            height: 12px;
            border-radius: 50%;
            background: var(--color-neutral);
        }

            .dot.correct {
                background: var(--color-correct);
            }

            .dot.wrong {
                background: var(--color-wrong);
            }

        .overview-score {
            font-weight: 700;
            font-size: 1.1rem;
            min-width: 32px;
            text-align: right;
        }

        /* ── Error Toast ──────────────────────────────────────────── */
        #toast {
            position: fixed;
            bottom: 24px;
            left: 50%;
            transform: translateX(-50%) translateY(100px);
            background: var(--color-wrong);
            color: #fff;
            padding: 12px 20px;
            border-radius: var(--radius);
            font-size: 0.9rem;
            transition: transform 0.3s;
            z-index: 999;
            white-space: nowrap;
        }

            #toast.show {
                transform: translateX(-50%) translateY(0);
            }

        /* ── Waiting State ────────────────────────────────────────── */
        .waiting {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            flex: 1;
            gap: 16px;
            color: var(--color-subtext);
            text-align: center;
            padding: 32px;
        }

        .spinner {
            width: 40px;
            height: 40px;
            border: 3px solid var(--color-card);
            border-top-color: var(--color-accent);
            border-radius: 50%;
            animation: spin 0.8s linear infinite;
        }

        @keyframes spin {
            to {
                transform: rotate(360deg);
            }
        }

        /* ── Done State ───────────────────────────────────────────── */
        .done-state {
            flex: 1;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 12px;
            padding: 32px;
            text-align: center;
        }

        .done-icon {
            font-size: 4rem;
        }

        .done-title {
            font-size: 1.5rem;
            font-weight: 700;
        }

        .done-sub {
            color: var(--color-subtext);
            font-size: 0.95rem;
        }
    </style>
</head>
<body>

    <div id="connection-status"></div>
    <div id="toast"></div>

    <div id="app">

        <!-- ── Screen: Connect ──────────────────────────────────────── -->
        <div id="screen-connect" class="screen active">
            <h1>PubQuizMaster</h1>
            <p>Enter your Scorer ID to join the session.</p>

            <div class="input-group">
                <label for="input-scorer-id">Scorer ID</label>
                <input id="input-scorer-id" type="text" placeholder="e.g. scorer-a" autocomplete="off"
                       autocapitalize="none" autocorrect="off" spellcheck="false" />
            </div>

            <button class="btn btn-primary" onclick="connect()">Connect</button>
        </div>

        <!-- -- Screen: Sort ------------------------------------------- -->
        <div id="screen-sort" class="screen">
            <div class="header">
                <span class="header-title" id="sort-round-name">Round –</span>
            </div>
            <div class="waiting" style="gap: 20px; align-items: flex-start; padding: 24px;">
                <div style="width:100%; text-align:center; color: var(--color-subtext); font-size: 0.9rem;">
                    Sort your answer sheets in this order:
                </div>
                <ol id="sort-team-list" style="
            list-style: none;
            width: 100%;
            display: flex;
            flex-direction: column;
            gap: 10px;
            padding: 0;
        "></ol>
            </div>
            <div style="padding: 12px 16px; background: var(--color-surface); border-top: 1px solid var(--color-card);">
                <button class="btn btn-primary" onclick="startScoring()">▶ Start Scoring</button>
            </div>
        </div>

        <!-- ── Screen: Scoring ──────────────────────────────────────── -->
        <div id="screen-scoring" class="screen">

            <div class="header">
                <span class="header-title" id="hdr-round">Round –</span>
                <span class="header-round" id="hdr-qcount">Q – / –</span>
                <button class="btn btn-secondary btn-sm" onclick="showOverview()">Overview</button>
            </div>

            <div class="progress-bar">
                <div class="progress-fill" id="progress-fill" style="width:0%"></div>
            </div>

            <!-- Waiting for round -->
            <div class="waiting" id="waiting-state">
                <div class="spinner"></div>
                <p>Waiting for the host to start a round…</p>
            </div>

            <!-- Active scoring -->
            <div id="scoring-active" style="display:none; flex-direction:column; flex:1;">

                <div class="question-info">
                    <div class="question-label">Question</div>
                    <div class="question-number" id="question-number">1</div>
                </div>

                <div class="team-area">
                    <div class="team-counter" id="team-counter">Team 1 of 10</div>
                    <div class="team-name" id="team-name">Team Name</div>
                    <div class="answer-indicator" id="answer-indicator">–</div>
                </div>

                <div class="answer-buttons">
                    <button class="btn-correct" onclick="submitAnswer(true)">✓</button>
                    <button class="btn-wrong" onclick="submitAnswer(false)">✗</button>
                </div>

            </div>

            <div class="nav-bar" id="nav-bar">
                <button class="btn btn-secondary" id="btn-prev" onclick="navPrev()">← Prev</button>
                <button class="btn btn-secondary" id="btn-next" onclick="navNext()">Next →</button>
                <button class="btn btn-secondary" id="btn-review" style="display:none;" onclick="goBackToScoring()">✎ Review Answers</button>
            </div>

        </div>

        <!-- ── Screen: Overview ─────────────────────────────────────── -->
        <div id="screen-overview" class="screen">
            <div class="header">
                <span class="header-title">Overview</span>
                <button class="btn btn-secondary btn-sm" id="overview-back-btn" onclick="showScoring()">← Back</button>
            </div>
            <div class="overview-scroll" id="overview-list"></div>
        </div>

    </div>

    <!-- SignalR JS client — served from wwwroot -->
    <script src="signalr.min.js"></script>

    <script>
        // ─────────────────────────────────────────────
        // STATE
        // ─────────────────────────────────────────────

        const state = {
            scorerId: null,
            connection: null,
            teams: [],
            questionCount: 0,
            roundId: null,
            roundName: null,
            answers: {},
            currentTeamIndex: 0,
            currentQuestionIndex: 0,
            reviewMode: false,
            roundStartedViaEvent: false,
            overviewIsEndScreen: false,
        };

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────

        function answerKey(teamIndex, questionIndex) {
            return `${teamIndex}_${questionIndex}`;
        }

        function getAnswer(teamIndex, questionIndex) {
            return state.answers[answerKey(teamIndex, questionIndex)] ?? null;
        }

        function setAnswer(teamIndex, questionIndex, value) {
            state.answers[answerKey(teamIndex, questionIndex)] = value;
        }

        function totalCorrect(teamIndex) {
            let count = 0;
            for (let q = 0; q < state.questionCount; q++) {
                if (getAnswer(teamIndex, q) === true) count++;
            }
            return count;
        }

        function showToast(msg, duration = 3000) {
            const el = document.getElementById('toast');
            el.textContent = msg;
            el.classList.add('show');
            setTimeout(() => el.classList.remove('show'), duration);
        }

        // ─────────────────────────────────────────────
        // SCREENS
        // ─────────────────────────────────────────────

        function showScreen(id) {
            document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
            document.getElementById(id).classList.add('active');
        }

        function showScoring() { showScreen('screen-scoring'); renderScoring(); }

        function showOverview(isEndScreen = false) {
            state.overviewIsEndScreen = isEndScreen;
            const btn = document.getElementById('overview-back-btn');
            if (isEndScreen) {
                btn.textContent = '✎ Review Answers';
                btn.onclick = goBackToScoring;
            } else {
                btn.textContent = '← Back';
                btn.onclick = showScoring;
            }
            showScreen('screen-overview');
            renderOverview();
        }

        // ─────────────────────────────────────────────
        // CONNECTION
        // ─────────────────────────────────────────────

        async function connect() {
            const scorerId = document.getElementById('input-scorer-id').value.trim();
            if (!scorerId) { showToast('Please enter a Scorer ID'); return; }

            state.scorerId = scorerId;
            setConnectionStatus('connecting');

            const hubUrl = `${window.location.origin}/quizhub?scorerId=${encodeURIComponent(scorerId)}`;

            state.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
                .build();

            // ── Hub event handlers ────────────────────────────────────────
            state.connection.on('ScorerConnected', (payload) => {
                state.teams = payload.teams ?? [];
                state.questionCount = payload.round?.questionCount ?? 0;
                state.roundId = payload.round?.id ?? null;
                state.roundName = payload.round?.name ?? null;
                state.answers = {};
                state.reviewMode = false;
                state.currentTeamIndex = 0;
                state.currentQuestionIndex = 0;

                if (payload.existingAnswers) {
                    for (const a of payload.existingAnswers) {
                        const teamIndex = state.teams.findIndex(t => t.id === a.teamId);
                        if (teamIndex >= 0) setAnswer(teamIndex, a.questionIndex, a.value?.correct ?? null);
                    }
                }

                const hasRound = !!state.roundId && state.teams.length > 0;
                const hasExistingAnswers = payload.existingAnswers && payload.existingAnswers.length > 0;

                if (hasRound && !hasExistingAnswers) {
                    // Fresh round — show sort screen first
                    showScreen('screen-sort');
                    renderSortScreen();
                } else {
                    // Reconnect or already in progress — go straight to scoring
                    showScreen('screen-scoring');
                    renderScoring();
                }
            });

            state.connection.on('OnAnswerUpdated', (payload) => {
                const teamIndex = state.teams.findIndex(t => t.id === payload.teamId);
                if (teamIndex >= 0) {
                    const correct = payload.value?.correct ?? null;
                    setAnswer(teamIndex, payload.questionIndex, correct);
                    renderScoring();
                }
            });

            state.connection.on('RoundStarted', async (payload) => {
                showToast(`Round started: ${payload.round?.name ?? ''}`);
                try {
                    await state.connection.invoke('RequestState');
                    // ScorerConnected will show sort screen (no existing answers yet)
                } catch (err) {
                    console.error('RequestState failed:', err);
                }
            });

            state.connection.on('RoundFinalized', () => {
                showToast('Round finalized by host.');
                renderScoring();
            });

            state.connection.on('Error', (payload) => {
                showToast(payload.message ?? 'Unknown error');
            });

            state.connection.onreconnecting(() => setConnectionStatus('connecting'));
            state.connection.onreconnected(() => {
                setConnectionStatus('connected');
                // Re-request state in case we missed updates
                state.connection.invoke('RequestState').catch(console.error);
            });
            state.connection.onclose(() => setConnectionStatus('disconnected'));

            // ── Start connection ──────────────────────────────────────────

            try {
                await state.connection.start();
                setConnectionStatus('connected');
            } catch (err) {
                setConnectionStatus('disconnected');
                showToast('Connection failed. Check the server address.');
                console.error(err);
            }
        }

        function setConnectionStatus(status) {
            const el = document.getElementById('connection-status');
            el.className = status === 'connected' ? 'connected'
                : status === 'connecting' ? 'connecting'
                    : '';
        }

        // ─────────────────────────────────────────────
        // RENDER
        // ─────────────────────────────────────────────

        function renderScoring() {
            const hasRound = state.roundId && state.teams.length > 0 && state.questionCount > 0;

            document.getElementById('waiting-state').style.display = hasRound ? 'none' : 'flex';

            if (!hasRound) return;

            const allDone = isRoundComplete() && !state.reviewMode;

            document.getElementById('scoring-active').style.display = allDone ? 'none' : 'flex';

            document.getElementById('btn-prev').style.display = allDone ? 'none' : '';
            document.getElementById('btn-next').style.display = allDone ? 'none' : '';
            document.getElementById('btn-review').style.display = allDone ? '' : 'none';

            // Header — always render, regardless of allDone
            document.getElementById('hdr-round').textContent = state.roundName ?? 'Round';
            document.getElementById('hdr-qcount').textContent =
                `Q ${state.currentQuestionIndex + 1} / ${state.questionCount}`;

            // Progress — always render
            const answered = Object.keys(state.answers).filter(k => state.answers[k] !== null).length;
            const total = state.teams.length * state.questionCount;
            const pct = total > 0 ? Math.round((answered / total) * 100) : 0;
            document.getElementById('progress-fill').style.width = pct + '%';

            // Disable Prev at first position, Next at last position
            const isFirst = state.currentTeamIndex === 0 && state.currentQuestionIndex === 0;
            const isLast = state.currentTeamIndex === state.teams.length - 1 &&
                state.currentQuestionIndex === state.questionCount - 1;
            document.getElementById('btn-prev').disabled = isFirst;
            document.getElementById('btn-next').disabled = isLast;

            if (allDone) {
                showOverview(true);
                return;
            }

            // Team info — only needed for active scoring
            const team = state.teams[state.currentTeamIndex];
            document.getElementById('question-number').textContent = state.currentQuestionIndex + 1;
            document.getElementById('team-name').textContent = team?.name ?? '–';
            document.getElementById('team-counter').textContent =
                `Team ${state.currentTeamIndex + 1} of ${state.teams.length}`;

            // Answer indicator
            const existing = getAnswer(state.currentTeamIndex, state.currentQuestionIndex);
            const indicator = document.getElementById('answer-indicator');
            if (existing === true) { indicator.textContent = '✓'; indicator.className = 'answer-indicator correct'; }
            else if (existing === false) { indicator.textContent = '✗'; indicator.className = 'answer-indicator wrong'; }
            else { indicator.textContent = '–'; indicator.className = 'answer-indicator'; }
        }

        function renderOverview() {
            const list = document.getElementById('overview-list');
            list.innerHTML = '';

            if (!state.roundId || state.teams.length === 0) {
                list.innerHTML = '<div class="waiting"><p>No round active.</p></div>';
                return;
            }

            for (let t = 0; t < state.teams.length; t++) {
                const team = state.teams[t];
                const row = document.createElement('div');
                row.className = 'overview-row';

                // Team name
                const nameEl = document.createElement('div');
                nameEl.className = 'overview-team';
                nameEl.textContent = team.name;

                // Dots
                const dots = document.createElement('div');
                dots.className = 'overview-dots';
                for (let q = 0; q < state.questionCount; q++) {
                    const dot = document.createElement('div');
                    const val = getAnswer(t, q);
                    dot.className = 'dot' + (val === true ? ' correct' : val === false ? ' wrong' : '');
                    dot.title = `Q${q + 1}`;
                    dots.appendChild(dot);
                }

                // Score
                const score = document.createElement('div');
                score.className = 'overview-score';
                score.textContent = totalCorrect(t);

                row.appendChild(nameEl);
                row.appendChild(dots);
                row.appendChild(score);
                list.appendChild(row);
            }
        }

        // ─────────────────────────────────────────────
        // NAVIGATION
        // ─────────────────────────────────────────────

        function navNext() {
            if (!state.roundId) return;

            if (state.currentTeamIndex < state.teams.length - 1) {
                state.currentTeamIndex++;
            } else if (state.currentQuestionIndex < state.questionCount - 1) {
                state.currentTeamIndex = 0;
                state.currentQuestionIndex++;
            } else if (state.reviewMode) {
                // End of review — back to done screen
                state.reviewMode = false;
                showOverview(true);
                return;
            } else {
                // Last team, last question, not in review — round complete
                showOverview(true);
                return;
            }

            renderScoring();
        }

        function navPrev() {
            if (!state.roundId) return;

            if (state.currentTeamIndex > 0) {
                state.currentTeamIndex--;
            } else if (state.currentQuestionIndex > 0) {
                state.currentQuestionIndex--;
                state.currentTeamIndex = state.teams.length - 1;
            }

            renderScoring();
        }

        function goBackToScoring() {
            state.reviewMode = true;
            state.currentQuestionIndex = state.questionCount - 1;
            state.currentTeamIndex = state.teams.length - 1;
            showScreen('screen-scoring');
            renderScoring();
        }

        // ─────────────────────────────────────────────
        // SCORING
        // ─────────────────────────────────────────────

        async function submitAnswer(correct) {
            if (!state.connection || !state.roundId) return;

            const team = state.teams[state.currentTeamIndex];
            if (!team) return;

            // Capture position before any async suspension
            const teamId = team.id;
            const teamIndex = state.currentTeamIndex;
            const questionIndex = state.currentQuestionIndex;

            // Advance immediately — next click already targets the correct cell
            setAnswer(teamIndex, questionIndex, correct);
            navNext();

            try {
                await state.connection.invoke('SubmitBoolAnswer',
                    state.roundId,
                    teamId,
                    questionIndex,
                    correct
                );
            } catch (err) {
                showToast('Failed to submit — check connection.');
                console.error(err);
            }
        }

        function showSortScreen(roundName) {
            state.roundStartedViaEvent = true;
            document.getElementById('sort-round-name').textContent = roundName;
            renderSortScreen();
            showScreen('screen-sort');
        }

        function renderSortScreen() {
            document.getElementById('sort-round-name').textContent = state.roundName ?? 'Round';
            const list = document.getElementById('sort-team-list');
            list.innerHTML = '';
            state.teams.forEach((team, i) => {
                const li = document.createElement('li');
                li.innerHTML = `
            <div class="sort-number">${i + 1}</div>
            <span>${team.name}</span>
        `;
                list.appendChild(li);
            });
        }

        function startScoring() {
            state.currentTeamIndex = 0;
            state.currentQuestionIndex = 0;
            showScreen('screen-scoring');
            renderScoring();
        }

        function isRoundComplete() {
            if (!state.roundId || state.teams.length === 0 || state.questionCount === 0) return false;
            for (let t = 0; t < state.teams.length; t++) {
                for (let q = 0; q < state.questionCount; q++) {
                    if (getAnswer(t, q) === null) return false;
                }
            }
            return true;
        }

        // ─────────────────────────────────────────────
        // KEYBOARD SUPPORT (for laptop scorers)
        // ─────────────────────────────────────────────

        document.addEventListener('keydown', (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

            const scoringActive = document.getElementById('screen-scoring').classList.contains('active');
            const overviewActive = document.getElementById('screen-overview').classList.contains('active');

            if (scoringActive) {
                if (e.key === 'ArrowRight') { e.preventDefault(); navNext(); }
                if (e.key === 'ArrowLeft') { e.preventDefault(); navPrev(); }
                if (e.key === 'y' || e.key === 'Y' || e.key === '1') { e.preventDefault(); submitAnswer(true); }
                if (e.key === 'n' || e.key === 'N' || e.key === '0') { e.preventDefault(); submitAnswer(false); }
                if (e.key === 'o' || e.key === 'O') { e.preventDefault(); showOverview(); }
            }
            if (overviewActive) {
                if (e.key === 'Escape' || e.key === 'b' || e.key === 'B') { e.preventDefault(); showScoring(); }
            }
        });

        // ─────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────

        // Auto-fill scorerId from URL query param: ?scorerId=scorer-a
        const urlParams = new URLSearchParams(window.location.search);
        const prefilledId = urlParams.get('scorerId');
        if (prefilledId) {
            document.getElementById('input-scorer-id').value = prefilledId;
        }
    </script>
</body>
</html>
```


## FILE: PubQuizMaster.sln

```sln

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.14.37111.16 d17.14
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PubQuizMaster", "Desktop\PubQuizMaster.Desktop.csproj", "{57162A5D-4CC9-425D-A5AB-ED87C6BCB7B6}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PubQuizMaster.Core", "Core\PubQuizMaster.Core.csproj", "{FD4596AA-895A-44E3-AB94-872DF1639C8F}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{57162A5D-4CC9-425D-A5AB-ED87C6BCB7B6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{57162A5D-4CC9-425D-A5AB-ED87C6BCB7B6}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{57162A5D-4CC9-425D-A5AB-ED87C6BCB7B6}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{57162A5D-4CC9-425D-A5AB-ED87C6BCB7B6}.Release|Any CPU.Build.0 = Release|Any CPU
		{FD4596AA-895A-44E3-AB94-872DF1639C8F}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{FD4596AA-895A-44E3-AB94-872DF1639C8F}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{FD4596AA-895A-44E3-AB94-872DF1639C8F}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{FD4596AA-895A-44E3-AB94-872DF1639C8F}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {E5DA85E9-B718-4FD0-B755-ABFF142E4E9B}
	EndGlobalSection
EndGlobal

```


