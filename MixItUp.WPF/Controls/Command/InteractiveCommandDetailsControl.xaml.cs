﻿using Mixer.Base.Model.Interactive;
using Mixer.Base.Util;
using MixItUp.Base;
using MixItUp.Base.Actions;
using MixItUp.Base.Commands;
using MixItUp.WPF.Util;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Command
{
    /// <summary>
    /// Interaction logic for InteractiveCommandDetailsControl.xaml
    /// </summary>
    public partial class InteractiveCommandDetailsControl : CommandDetailsControlBase
    {
        private InteractiveGameListingModel game;
        private InteractiveGameVersionModel version;
        private InteractiveSceneModel scene;
        private InteractiveControlModel control;

        private InteractiveCommand command;

        public InteractiveCommandDetailsControl(InteractiveGameListingModel game, InteractiveGameVersionModel version, InteractiveSceneModel scene, InteractiveCommand command)
            : this(game, version, scene, command.Control)
        {
            this.command = command;
        }

        public InteractiveCommandDetailsControl(InteractiveGameListingModel game, InteractiveGameVersionModel version, InteractiveSceneModel scene, InteractiveControlModel control)
        {
            this.game = game;
            this.version = version;
            this.scene = scene;
            this.control = control;

            InitializeComponent();
        }

        public override Task Initialize()
        {
            this.ButtonTriggerComboBox.ItemsSource = EnumHelper.GetEnumNames<InteractiveButtonCommandTriggerType>();
            this.CooldownTypeComboBox.ItemsSource = new List<string>() { "Individual", "Group" };
            this.CooldownGroupsComboBox.ItemsSource = ChannelSession.Settings.InteractiveCooldownGroups.Keys;

            if (this.control != null && this.control is InteractiveButtonControlModel)
            {
                this.ButtonTriggerComboBox.IsEnabled = true;
                this.ButtonTriggerComboBox.SelectedItem = EnumHelper.GetEnumName(InteractiveButtonCommandTriggerType.MouseDown);
                this.SparkCostTextBox.IsEnabled = true;
                this.SparkCostTextBox.Text = ((InteractiveButtonControlModel)this.control).cost.ToString();
                this.CooldownGroupsComboBox.IsEnabled = true;
                this.CooldownTypeComboBox.IsEnabled = true;
                this.CooldownTextBox.IsEnabled = true;
            }

            if (this.command != null)
            {
                if (this.command.Button != null)
                {
                    this.ButtonTriggerComboBox.SelectedItem = EnumHelper.GetEnumName(this.command.Trigger);
                    if (!string.IsNullOrEmpty(this.command.CooldownGroup))
                    {
                        this.CooldownTypeComboBox.SelectedItem = "Group";
                        this.CooldownGroupsComboBox.SelectedItem = this.command.CooldownGroup;
                        this.CooldownTextBox.Text = ChannelSession.Settings.InteractiveCooldownGroups[this.command.CooldownGroup].ToString();
                    }
                    else
                    {
                        this.CooldownTypeComboBox.SelectedItem = "Individual";
                        this.CooldownTextBox.Text = this.command.IndividualCooldown.ToString();
                    }
                }
            }

            return Task.FromResult(0);
        }

        public override IEnumerable<ActionTypeEnum> GetAllowedActions() { return InteractiveCommand.AllowedActions; }

        public override bool Validate()
        {
            int sparkCost = 0;
            int cooldown = 0;

            if (this.control is InteractiveButtonControlModel)
            {
                if (this.ButtonTriggerComboBox.SelectedIndex < 0)
                {
                    MessageBoxHelper.ShowDialog("An trigger type must be selected");
                    return false;
                }

                if (!int.TryParse(this.SparkCostTextBox.Text, out sparkCost) || sparkCost < 0)
                {
                    MessageBoxHelper.ShowDialog("A valid spark cost must be entered");
                    return false;
                }

                if (this.CooldownTypeComboBox.SelectedIndex < 0)
                {
                    MessageBoxHelper.ShowDialog("A cooldown type must be selected");
                    return false;
                }

                if (this.CooldownTypeComboBox.SelectedItem.Equals("Group") && this.CooldownGroupsComboBox.SelectedIndex < 0 && string.IsNullOrEmpty(this.CooldownGroupsComboBox.Text))
                {
                    MessageBoxHelper.ShowDialog("A cooldown group must be selected or entered");
                    return false;
                }

                if (!string.IsNullOrEmpty(this.CooldownTextBox.Text))
                {
                    if (!int.TryParse(this.CooldownTextBox.Text, out cooldown) || cooldown < 0)
                    {
                        MessageBoxHelper.ShowDialog("Cooldown must be 0 or greater");
                        return false;
                    }
                }
            }

            return true;
        }

        public override CommandBase GetExistingCommand() { return this.command; }

        public override async Task<CommandBase> GetNewCommand()
        {
            if (this.Validate())
            {
                InteractiveButtonCommandTriggerType trigger = EnumHelper.GetEnumValueFromString<InteractiveButtonCommandTriggerType>((string)this.ButtonTriggerComboBox.SelectedItem);
                if (this.command == null)
                {
                    if (this.control is InteractiveButtonControlModel)
                    {                
                        this.command = new InteractiveCommand(this.game, this.scene, (InteractiveButtonControlModel)this.control, trigger);
                    }
                    else
                    {
                        this.command = new InteractiveCommand(this.game, this.scene, (InteractiveJoystickControlModel)this.control);
                    }
                    ChannelSession.Settings.InteractiveControls.Add(this.command);
                }

                if (this.control is InteractiveButtonControlModel)
                {
                    this.command.Trigger = trigger;
                    this.command.Button.cost = int.Parse(this.SparkCostTextBox.Text);
                    if (this.CooldownTypeComboBox.SelectedItem.Equals("Group"))
                    {
                        string cooldownGroup = this.CooldownGroupsComboBox.Text;
                        this.command.CooldownGroup = cooldownGroup;
                        ChannelSession.Settings.InteractiveCooldownGroups[cooldownGroup] = int.Parse(this.CooldownTextBox.Text);
                    }
                    else
                    {
                        int cooldown = 0;
                        if (!string.IsNullOrEmpty(this.CooldownTextBox.Text))
                        {
                            cooldown = int.Parse(this.CooldownTextBox.Text);
                        }
                        this.command.IndividualCooldown = cooldown;
                    }

                    await ChannelSession.MixerConnection.Interactive.UpdateInteractiveGameVersion(this.version);
                }
                return this.command;
            }
            return null;
        }

        private void CooldownTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.CooldownTypeComboBox.SelectedIndex >= 0)
            {
                string selection = (string)this.CooldownTypeComboBox.SelectedItem;
                if (selection.Equals("Group"))
                {
                    this.CooldownGroupsComboBox.Visibility = Visibility.Visible;
                }
                else if (selection.Equals("Individual"))
                {
                    this.CooldownGroupsComboBox.Visibility = Visibility.Collapsed;
                }
                this.CooldownGroupsComboBox.SelectedIndex = -1;
                this.CooldownTextBox.Clear();
            }
        }

        private void CooldownGroupsComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string selection = (string)e.AddedItems[0];
                if (ChannelSession.Settings.InteractiveCooldownGroups.ContainsKey(selection))
                {
                    this.CooldownTextBox.Text = ChannelSession.Settings.InteractiveCooldownGroups[selection].ToString();
                }
                else
                {
                    this.CooldownTextBox.Text = string.Empty;
                }
            }
        }
    }
}