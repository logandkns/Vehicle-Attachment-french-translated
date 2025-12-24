/*
 * Inferno Collection Vehicle Attachment 1.5 Beta
 * 
 * Copyright (c) 2019-2021, Christopher M, Inferno Collection. All rights reserved.
 * 
 * This project is licensed under the following:
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * The software may not be sold in any format.
 * Modified copies of the software may only be shared in an uncompiled format.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using CitizenFX.Core.Native;
using InfernoCollection.VehicleAttachment.Client.Models;

namespace InfernoCollection.VehicleCollection.Client
{
    public class Main : BaseScript
    {
        #region Configuration Variables
        internal readonly Vector3
            POSITION_VECTOR = new Vector3(0.0f, -2.0f, 1.5f),
            ROTATION_VECTOR = new Vector3(0.0f, 0.0f, 0.0f),
            RAYCAST_VECTOR = new Vector3(0.0f, 2.0f, 0.0f);

        internal const string
            CONFIG_FILE_NAME = "config.json",
            TOW_CONTROLS =
                "~INPUT_F8DD5118~/~INPUT_2F20FA6E~ = Avancer/Reculer" +
                "\n~INPUT_872241C1~/~INPUT_DEEBB52A~ = Gauche/Droite" +
                "\n~INPUT_32D078AF~/~INPUT_7B7B256B~ = haut/Bas" +
                "\n~INPUT_6DC8415B~/~INPUT_4EEC321F~ = Rotation Gauche/Droite" +
                "\n~INPUT_83B8F159~/~INPUT_EE722E7A~ = Rotation Haut/Bas" +
                "\nHold ~INPUT_SPRINT~/~INPUT_DUCK~ = Accélérer/Ralentir" +
                "\n~INPUT_94172EE1~ = Confirmer Position";
        #endregion

        #region General Variables
        internal bool
            _driveOn,
            _goFaster,
            _goSlower;

        internal Vehicle
            _tempTowVehicle,
            _tempVehicleBeingTowed;

        internal Config _config = new Config();

        internal AttachmentStage _attachmentStage;
        internal AttachmentStage _previousAttachmentStage;
        #endregion

        #region Constructor
        public Main()
        {
            Game.PlayerPed.State.Set("oneSyncTest", "test", true);
            if (Game.PlayerPed.State.Get("oneSyncTest") == null)
            {
                throw new Exception("ette ressource nécessite au moins OneSync \"legacy\". Utilisez la version bêta publique 1.3 si vous ne souhaitez pas utiliser OneSync.");
            }

            TriggerEvent("chat:addSuggestion", "/attacher [driveon|help|annuler]", "Lance le processus d'attelage d'un véhicule à un autre.");
            TriggerEvent("chat:addSuggestion", "/detacher [help|annuler]", "Commence le processus de détachement d'un véhicule d'un autre.");

            #region Key Mapping
            API.RegisterKeyMapping("inferno-vehicle-attachment-forward", "Déplacer le véhicule attelé vers l'avant.", "keyboard", "NUMPAD8"); // ~INPUT_F8DD5118~
            API.RegisterKeyMapping("inferno-vehicle-attachment-back", "Reculer le véhicule attelé.", "keyboard", "NUMPAD5"); // ~INPUT_2F20FA6E~
            API.RegisterKeyMapping("inferno-vehicle-attachment-left", "Déplacer le véhicule attelé vers la gauche.", "keyboard", "NUMPAD4"); // ~INPUT_872241C1~
            API.RegisterKeyMapping("inferno-vehicle-attachment-right", "Déplacer le véhicule attelé vers la droite", "keyboard", "NUMPAD6"); // ~INPUT_DEEBB52A~
            API.RegisterKeyMapping("inferno-vehicle-attachment-up", "Déplacer le véhicule attelé vers le haut.", "keyboard", "PAGEUP"); // ~INPUT_32D078AF~
            API.RegisterKeyMapping("inferno-vehicle-attachment-down", "Déplacer le véhicule attelé vers le bas.", "keyboard", "PAGEDOWN"); // ~INPUT_7B7B256B~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-left", "Tourner le véhicule attelé vers la gauche.", "keyboard", "NUMPAD7"); // ~INPUT_6DC8415B~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-right", "Tourner le véhicule attelé vers la droite.", "keyboard", "NUMPAD9"); /// ~INPUT_4EEC321F~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-up", "Pivoter le véhicule attaché vers le haut.", "keyboard", "INSERT"); // ~INPUT_83B8F159~
            API.RegisterKeyMapping("inferno-vehicle-attachment-rotate-down", "Pivoter le véhicule attaché vers le bas.", "keyboard", "DELETE"); // ~INPUT_EE722E7A~
            API.RegisterKeyMapping("inferno-vehicle-attachment-confirm", "Confirmer le véhicule attaché.", "keyboard", "NUMPADENTER"); // ~INPUT_CAAAA4F4~
            #endregion

            #region Load configuration file
            string ConfigFile = null;

            try
            {
                ConfigFile = API.LoadResourceFile("inferno-vehicle-attachment", CONFIG_FILE_NAME);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Erreur lors du chargement de la configuration à partir du fichier, impossible de charger le contenu du fichier. Retour aux valeurs de configuration par défaut.");
                Debug.WriteLine(exception.ToString());
            }

            if (ConfigFile != null && ConfigFile != "")
            {
                try
                {
                    _config = JsonConvert.DeserializeObject<Config>(ConfigFile);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine("Erreur lors du chargement de la configuration à partir du fichier, le contenu n'est pas valide. Retour aux valeurs de configuration par défaut.");
                    Debug.WriteLine(exception.ToString());
                }
            }
            else
            {
                Debug.WriteLine("Le fichier de configuration chargé est vide, retour aux paramètres par défaut.");
            }
            #endregion
        }
        #endregion

        #region Command Handlers
        #region Attach/detach
        /// <summary>
        /// Triggers event that starts the attaching process.
        /// Also handles the triggering of the canceling process, and showing the help information.
        /// </summary>
        /// <param name="args">Command arguments</param>
        [Command("attacher")]
        internal void OnAttach(string[] args)
        {
            if (args.Count() > 0)
            {
                if (args[0] == "help")
                {
                    ShowTowControls();
                }
                else if (args[0] == "driveon")
                {
                    _driveOn = !_driveOn;

                    Screen.ShowNotification($"~o~Mode Drive On {(_driveOn ? "~g~activé" : "~r~désactivé")}", true);
                }
                else if (args[0] == "annuler")
                {
                    if (_attachmentStage != AttachmentStage.None)
                    {
                        _previousAttachmentStage = _attachmentStage;
                        _attachmentStage = AttachmentStage.Cancel;

                        Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                    }
                    else
                    {
                        Screen.ShowNotification("~r~Vous n'êtes pas en train d'interagir avec un véhicule pour le moment !");
                    }
                }
            }
            else
            {
                OnNewAttachment();
            }
        }

        /// <summary>
        /// Triggers event that starts the detaching process.
        /// Also handles the triggering of the canceling process, and showing the help information.
        /// </summary>
        /// <param name="args">Command arguments</param>
        [Command("detacher")]
        internal void OnDetach(string[] args)
        {
            if (args.Count() > 0)
            {
                if (args[0] == "help")
                {
                    ShowTowControls();
                }
                else if (args[0] == "annuler")
                {
                    if (_attachmentStage != AttachmentStage.None)
                    {
                        _previousAttachmentStage = _attachmentStage;
                        _attachmentStage = AttachmentStage.Cancel;

                        Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                    }
                    else
                    {
                        Screen.ShowNotification("~r~Vous n'êtes pas en train d'interagir avec un véhicule pour le moment !");
                    }
                }
            }
            else
            {
                OnRemoveLastAttachment();
            }
        }
        #endregion

        #region Controls
        [Command("inferno-vehicle-attachment-forward")]
        internal void OnForward() => OnControl(AttachmentControl.Forward);

        [Command("inferno-vehicle-attachment-back")]
        internal void OnBack() => OnControl(AttachmentControl.Back);

        [Command("inferno-vehicle-attachment-left")]
        internal void OnLeft() => OnControl(AttachmentControl.Left);

        [Command("inferno-vehicle-attachment-right")]
        internal void OnRight() => OnControl(AttachmentControl.Right);

        [Command("inferno-vehicle-attachment-up")]
        internal void OnUp() => OnControl(AttachmentControl.Up);

        [Command("inferno-vehicle-attachment-down")]
        internal void OnDown() => OnControl(AttachmentControl.Down);

        [Command("inferno-vehicle-attachment-rotate-left")]
        internal void OnRotateLeft() => OnControl(AttachmentControl.RotateLeft);

        [Command("inferno-vehicle-attachment-rotate-right")]
        internal void OnRotateRight() => OnControl(AttachmentControl.RotateRight);

        [Command("inferno-vehicle-attachment-rotate-up")]
        internal void OnRotateUp() => OnControl(AttachmentControl.RotateUp);

        [Command("inferno-vehicle-attachment-rotate-down")]
        internal void OnRotateDown() => OnControl(AttachmentControl.RotateDown);

        [Command("inferno-vehicle-attachment-confirm")]
        internal void OnConfirm() => OnControl(AttachmentControl.Confirm);
        #endregion
        #endregion

        #region Event Handlers
        /// <summary>
        /// Starts the process of attaching a <see cref="Vehicle"/> to another <see cref="Vehicle"/>
        /// </summary>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:NewAttachment")]
        internal void OnNewAttachment()
        {
            if (_attachmentStage != AttachmentStage.None)
            {
                Screen.ShowNotification("~r~Vous êtes déjà en interaction avec un autre véhicule !");
            }
            else
            {
                _attachmentStage = AttachmentStage.TowTruck;
                Tick += AttachmentTick;

                Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                Screen.ShowNotification("~g~Sélectionnez le véhicule qui effectuera le remorquage pour commencer.");
            }
        }

        /// <summary>
        /// Starts the process of detaching one <see cref="Vehicle"/> from <see cref="Vehicle"/> vehicle
        /// </summary>
        [EventHandler("Inferno-Collection:Vehicle-Attachment:RemoveLastAttachment")]
        internal void OnRemoveLastAttachment()
        {
            if (Entity.Exists(_tempTowVehicle) || Entity.Exists(_tempVehicleBeingTowed))
            {
                Screen.ShowNotification("~o~Utilisez \"/attacher annuler\" pour annuler.");
            }
            else
            {
                Vehicle towVehicle = World.GetAllVehicles()
                    .Where(i => Entity.Exists(i) && i.Position.DistanceToSquared(Game.PlayerPed.Position) <= _config.MaxDistanceFromTowVehicle)
                    .OrderBy(i => i.Position.DistanceToSquared(Game.PlayerPed.Position))
                    .FirstOrDefault(i => GetTowedVehicles(i).Count() > 0);

                if (_attachmentStage != AttachmentStage.None)
                {
                    Screen.ShowNotification("~r~Vous êtes déjà en interaction avec un autre véhicule !");
                }
                else if (!Entity.Exists(towVehicle))
                {
                    Screen.ShowNotification("~r~Aucun véhicule adapté trouvé !", true);
                }
                else
                {
                    List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

                    TowedVehicle towedVehicle = towedVehicles.Last();

                    Vehicle vehicleBeingTowed = (Vehicle)Entity.FromNetworkId(towedVehicle.NetworkId);

                    if (!Entity.Exists(vehicleBeingTowed))
                    {
                        Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                        Screen.ShowNotification("~r~Véhicule en cours de remorquage supprimé !");
                    }
                    else
                    {
                        _tempTowVehicle = towVehicle;
                        _tempVehicleBeingTowed = vehicleBeingTowed;

                        Game.PlaySound("TOGGLE_ON", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                        if (_driveOn)
                        {
                            Screen.ShowNotification($"~g~{_tempVehicleBeingTowed.LocalizedName ?? "Vehicle"} détaché, conduisez-le.");

                            ResetTowedVehicle(_tempVehicleBeingTowed);
                            SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);
                            RemoveTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed);

                            _tempTowVehicle = null;
                            _tempVehicleBeingTowed = null;

                            Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                        }
                        else
                        {
                            ShowTowControls();

                            _tempVehicleBeingTowed.Opacity = 225;

                            _attachmentStage = AttachmentStage.Detach;
                            Tick += AttachmentTick;

                            Screen.ShowNotification("~g~Suivez les instructions pour détacher le véhicule.");
                        }                        
                    }
                }
            }
        }
        #endregion

        #region Tick Handlers
        /// <summary>
        /// Handles vehicle selection, attaching, detaching, and canceling
        /// </summary>
        internal async Task AttachmentTick()
        {
            switch (_attachmentStage)
            {
                #region Selecting tow truck
                case AttachmentStage.TowTruck:
                    {
                        Vehicle towTruck = FindVehicle();

                        if (towTruck == null)
                        {
                            Screen.DisplayHelpTextThisFrame("Aucun véhicule trouvé !");
                        }
                        else if (IsAlreadyBeingUsed(towTruck))
                        {
                            Screen.DisplayHelpTextThisFrame($"Quelqu'un d'autre utilise le véhicule : {towTruck.LocalizedName ?? "tow truck"}.");
                        }
                        else if (
                            (!_config.BlacklistToWhitelist && _config.AttachmentBlacklist.Contains(towTruck.Model)) ||
                            (_config.BlacklistToWhitelist && !_config.AttachmentBlacklist.Contains(towTruck.Model))
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"Le véhicule : {towTruck.LocalizedName ?? "tow truck"} ne peut pas être utilisé comme véhicule de remorquage !");
                        }
                        else if (_config.MaxNumberOfAttachedVehicles > 0 && GetTowedVehicles(towTruck).Count() >= _config.MaxNumberOfAttachedVehicles)
                        {
                            Screen.DisplayHelpTextThisFrame($"Le véhicule : {towTruck.LocalizedName ?? "tow truck"} ne peut remorquer plus de véhicules !");
                        }
                        else
                        {
                            if (_config.EnableLine)
                            {
                                World.DrawLine(Game.PlayerPed.Position, towTruck.Position, System.Drawing.Color.FromArgb(255, 0, 255, 0));
                            }

                            Screen.DisplayHelpTextThisFrame($"~INPUT_FRONTEND_ACCEPT~ pour utiliser le véhicule : {towTruck.LocalizedName ?? "tow truck"} en tant que véhicule de remorquage.");

                            if (Game.IsControlJustPressed(0, Control.FrontendAccept))
                            {
                                Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                                Screen.ShowNotification($"Le véhicule : ~g~{towTruck.LocalizedName ?? "tow truck"} est confirmé comme véhicule de remorquage ! Sélectionnez maintenant un véhicule à remorquer.");

                                _tempTowVehicle = towTruck;
                                _attachmentStage = AttachmentStage.VehicleToBeTowed;

                                SetVehicleAsBeingUsed(towTruck, true);

                                await Delay(1000);
                            }
                        }
                    }
                    break;
                #endregion

                #region Selecting vehicle to be towed
                case AttachmentStage.VehicleToBeTowed:
                    {
                        Vehicle vehicleToBeTowed = FindVehicle();

                        if (vehicleToBeTowed == null)
                        {
                            Screen.DisplayHelpTextThisFrame("Aucun véhicule pouvant être remorqué n'a été trouvé !");
                        }
                        else if (!Entity.Exists(_tempTowVehicle))
                        {
                            _attachmentStage = AttachmentStage.Cancel;

                            Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                            Screen.ShowNotification("~r~Le véhicule remorqueur à été supprimé, impossible à atteler à quoi que ce soit !");
                        }
                        else if (IsAlreadyBeingUsed(vehicleToBeTowed))
                        {
                            Screen.DisplayHelpTextThisFrame($"Le véhicule : {vehicleToBeTowed.LocalizedName ?? "vehicle"} est déjà utilisé.");
                        }
                        else if (
                            (!_config.BlacklistToWhitelist && _config.AttachmentBlacklist.Contains(vehicleToBeTowed.Model)) ||
                            (_config.BlacklistToWhitelist && _config.WhitelistForTowedVehicles && !_config.AttachmentBlacklist.Contains(vehicleToBeTowed.Model))
                        )
                        {
                            Screen.DisplayHelpTextThisFrame($"Le véhicule : {vehicleToBeTowed.LocalizedName ?? "vehicle"} ne peut pas être remorqué !!");
                        }
                        else if (vehicleToBeTowed.Occupants.Length > 0)
                        {
                            Screen.DisplayHelpTextThisFrame($"Le véhicule : {vehicleToBeTowed.LocalizedName ?? "vehicle"} est occupé !");
                        }
                        else if (Entity.Exists(_tempTowVehicle) && vehicleToBeTowed.Position.DistanceToSquared2D(_tempTowVehicle.Position) > _config.MaxDistanceFromTowVehicle)
                        {
                            Screen.DisplayHelpTextThisFrame($"Le véhicule : {vehicleToBeTowed.LocalizedName ?? "vehicle"} est trop loin du véhicule : {_tempTowVehicle.LocalizedName ?? "tow truck"}!");
                        }
                        else
                        {
                            if (_config.EnableLine)
                            {
                                World.DrawLine(Game.PlayerPed.Position, vehicleToBeTowed.Position, System.Drawing.Color.FromArgb(255, 0, 255, 0));
                            }

                            Screen.DisplayHelpTextThisFrame($"~INPUT_FRONTEND_ACCEPT~ pour remorquer le véhicule : {vehicleToBeTowed.LocalizedName ?? "vehicle"}.");

                            if (Game.IsControlJustPressed(0, Control.FrontendAccept))
                            {
                                SetVehicleAsBeingUsed(vehicleToBeTowed, true);

                                int timeout = 4;
                                API.NetworkRequestControlOfNetworkId(vehicleToBeTowed.NetworkId);
                                while (!API.NetworkHasControlOfNetworkId(vehicleToBeTowed.NetworkId) && timeout > 0)
                                {
                                    timeout--;

                                    API.NetworkRequestControlOfNetworkId(vehicleToBeTowed.NetworkId);
                                    await Delay(250);
                                }

                                if (!API.NetworkHasControlOfNetworkId(vehicleToBeTowed.NetworkId))
                                {
                                    Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                                    Screen.ShowNotification($"~r~Impossible de remorquer le véhicule : {vehicleToBeTowed.LocalizedName ?? "vehicle"}.", true);

                                    Debug.WriteLine($"Impossible de remorquer le véhicule : {vehicleToBeTowed.LocalizedName} ({vehicleToBeTowed.NetworkId}); la propriété du véhicule ne pouvait pas être demandée !");

                                    _attachmentStage = AttachmentStage.Cancel;
                                }
                                else
                                {
                                    Game.PlaySound("OK", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                                    if (_driveOn)
                                    {
                                        Screen.ShowNotification($"Le véhicule : ~g~{vehicleToBeTowed.LocalizedName ?? "vehicle"} a été identifié comme devant être remorqué ! Placez le et confirmez.");

                                        vehicleToBeTowed.IsPersistent = true;

                                        AddNewTowedVehicle(_tempTowVehicle, new TowedVehicle() { NetworkId = vehicleToBeTowed.NetworkId });

                                        _tempVehicleBeingTowed = vehicleToBeTowed;
                                        _attachmentStage = AttachmentStage.DriveOn;

                                        await Delay(1000);
                                    }
                                    else
                                    {
                                        Screen.ShowNotification($"Le véhicule : ~g~{vehicleToBeTowed.LocalizedName ?? "vehicle"} a été identifié comme devant être remorqué ! Suivez les instructions pour positionner le véhicule.");

                                        ShowTowControls();

                                        vehicleToBeTowed.Opacity = 225;
                                        vehicleToBeTowed.IsPersistent = true;
                                        vehicleToBeTowed.IsPositionFrozen = true;
                                        vehicleToBeTowed.IsCollisionEnabled = false;
                                        vehicleToBeTowed.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
                                        vehicleToBeTowed.AttachTo(_tempTowVehicle, POSITION_VECTOR, ROTATION_VECTOR);

                                        AddNewTowedVehicle(_tempTowVehicle, new TowedVehicle()
                                        {
                                            NetworkId = vehicleToBeTowed.NetworkId,
                                            AttachmentPosition = POSITION_VECTOR,
                                            AttachmentRotation = ROTATION_VECTOR
                                        });

                                        _tempVehicleBeingTowed = vehicleToBeTowed;
                                        _attachmentStage = AttachmentStage.Position;

                                        await Delay(1000);
                                    }
                                }
                            }
                        }
                    }
                    break;
                #endregion

                #region Cancel current attachment
                case AttachmentStage.Cancel:
                    {
                        if (Entity.Exists(_tempTowVehicle))
                        {
                            if (!Entity.Exists(_tempVehicleBeingTowed))
                            {
                                SetVehicleAsBeingUsed(_tempTowVehicle, false);

                                Screen.ShowNotification("~g~Attachment canceled.");
                                Tick -= AttachmentTick;
                                _attachmentStage = AttachmentStage.None;
                            }
                            else
                            {
                                if (_tempTowVehicle.Position.DistanceToSquared2D(Game.PlayerPed.Position) > _config.MaxDistanceFromTowVehicle)
                                {
                                    Screen.ShowNotification($"Le véhicule : ~r~{_tempTowVehicle.LocalizedName ?? "Tow truck"} est trop loin !", true);
                                }
                                else
                                {
                                    ResetTowedVehicle(_tempVehicleBeingTowed);
                                    SetVehicleAsBeingUsed(_tempTowVehicle, false);
                                    SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);
                                    RemoveTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed);

                                    Screen.ShowNotification("~g~Remorquage annulé.");
                                    Tick -= AttachmentTick;
                                    _attachmentStage = AttachmentStage.None;
                                }
                            }
                        }
                        else
                        {
                            Screen.ShowNotification("~g~Remorquage annulé.");
                            Tick -= AttachmentTick;
                            _attachmentStage = AttachmentStage.None;
                        }
                    }
                    break;
                #endregion

                #region Drive On
                case AttachmentStage.DriveOn:
                    Screen.DisplayHelpTextThisFrame("~INPUT_FRONTEND_RDOWN~ pour confirmer la position");
                    break;
                #endregion

                #region Position/Detach
                default:
                    if (Game.IsControlPressed(0, Control.Sprint))
                    {
                        _goFaster = true;
                        _goSlower = false;
                        break;
                    }
                    else if (Game.IsControlPressed(0, Control.Duck))
                    {
                        _goFaster = false;
                        _goSlower = true;
                        break;
                    }

                    _goFaster = false;
                    _goSlower = false;
                    break;
                #endregion
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// Returns the <see cref="Vehicle"/> in front of the player
        /// </summary>
        /// <returns><see cref="Vehicle"/> in front of player</returns>
        internal Vehicle FindVehicle()
        {
            RaycastResult raycast = World.RaycastCapsule(Game.PlayerPed.Position, Game.PlayerPed.GetOffsetPosition(RAYCAST_VECTOR), 0.3f, (IntersectOptions)10, Game.PlayerPed);

            if (!raycast.DitHitEntity || !Entity.Exists(raycast.HitEntity) || !raycast.HitEntity.Model.IsVehicle)
            {
                return null;
            }

            return (Vehicle)raycast.HitEntity;
        }

        /// <summary>
        /// Properly detaches and resets a <see cref="Vehicle"/> that is attached to another <see cref="Vehicle"/>
        /// </summary>
        /// <param name="entity">Vehicle to reset in entity form</param>
        internal async void ResetTowedVehicle(Entity entity)
        {
            Vector3 position;
            Vehicle vehicle = (Vehicle)entity;

            vehicle.Opacity = 0;
            vehicle.Detach();

            if (!_driveOn)
            {
                position = vehicle.Position;

                vehicle.PlaceOnGround();
                vehicle.IsCollisionEnabled = true;
                vehicle.IsPositionFrozen = false;

                await Delay(1000);

                vehicle.Position = position;
            }

            vehicle.ResetOpacity();
            vehicle.LockStatus = VehicleLockStatus.Unlocked;
            vehicle.ApplyForce(new Vector3(0.0f, 0.0f, 0.001f));
        }

        /// <summary>
        /// Prints the tow controls to the chat box
        /// </summary>
        internal void ShowTowControls()
        {
            if (_config.EnableInstructions)
            {
                API.BeginTextCommandDisplayHelp("CELL_EMAIL_BCON");

                foreach (string s in Screen.StringToArray(TOW_CONTROLS))
                {
                    API.AddTextComponentSubstringPlayerName(s);
                }

                API.EndTextCommandDisplayHelp(0, false, true, _config.InstructionDisplayTime);
            }
        }

        /// <summary>
        /// Handles the control input from the keybind maps
        /// </summary>
        /// <param name="attachmentControl"></param>
        internal void OnControl(AttachmentControl attachmentControl)
        {
            if (_attachmentStage != AttachmentStage.Position && _attachmentStage != AttachmentStage.Detach)
            {
                if (_attachmentStage != AttachmentStage.DriveOn || attachmentControl != AttachmentControl.Confirm)
                {
                    return;
                }

                TowedVehicle towedVehicle = GetTowedVehicles(_tempTowVehicle).Last();

                if (_tempTowVehicle.Position.DistanceToSquared(_tempVehicleBeingTowed.Position) > _config.MaxDistanceFromTowVehicle)
                {
                    Screen.ShowNotification("~r~Impossible de l'attacher ici, trop loin du véhicule remorqueur", true);
                    return;
                }

                if (Game.PlayerPed.CurrentVehicle == _tempVehicleBeingTowed)
                {
                    Game.PlayerPed.Task.LeaveVehicle();
                }

                Vector3
                    position = _tempTowVehicle.GetPositionOffset(_tempVehicleBeingTowed.Position),
                    rotation = _tempVehicleBeingTowed.Rotation - _tempTowVehicle.Rotation;

                _tempVehicleBeingTowed.LockStatus = VehicleLockStatus.CannotBeTriedToEnter;
                _tempVehicleBeingTowed.AttachTo(_tempTowVehicle, position, rotation);

                TowedVehicle updatedTowedVehicle = new TowedVehicle()
                {
                    NetworkId = towedVehicle.NetworkId,
                    AttachmentPosition = position,
                    AttachmentRotation = rotation
                };

                UpdateTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed, updatedTowedVehicle);

                SetVehicleAsBeingUsed(_tempTowVehicle, false);
                SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);

                Screen.ShowNotification("~g~Remorquage réussi ! Conduisez prudemment.");

                _tempTowVehicle = null;
                _tempVehicleBeingTowed = null;

                Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                Tick -= AttachmentTick;
                _attachmentStage = AttachmentStage.None;
                return;
            }

            float changeAmount = _config.ChangeAmount;

            changeAmount += _goFaster ? _config.FasterAmount : _goSlower ? _config.SlowerAmount : 0f;

            if (!Entity.Exists(_tempTowVehicle) || !Entity.Exists(_tempVehicleBeingTowed))
            {
                Game.PlaySound("CANCEL", "HUD_FREEMODE_SOUNDSET");
                Screen.ShowNotification("~g~Remorquage annulé.");

                _attachmentStage = AttachmentStage.Cancel;
            }
            else
            {
                TowedVehicle towedVehicle = GetTowedVehicles(_tempTowVehicle).Last();

                Vector3
                    position = towedVehicle.AttachmentPosition,
                    rotation = towedVehicle.AttachmentRotation;

                switch (attachmentControl)
                {
                    case AttachmentControl.Forward:
                        position.Y += changeAmount;
                        break;

                    case AttachmentControl.Back:
                        position.Y -= changeAmount;
                        break;

                    case AttachmentControl.Left:
                        position.X -= changeAmount;
                        break;

                    case AttachmentControl.Right:
                        position.X += changeAmount;
                        break;

                    case AttachmentControl.Up:
                        position.Z += changeAmount;
                        break;

                    case AttachmentControl.Down:
                        position.Z -= changeAmount;
                        break;

                    case AttachmentControl.RotateLeft:
                        rotation.Z += changeAmount * 10;
                        break;

                    case AttachmentControl.RotateRight:
                        rotation.Z -= changeAmount * 10;
                        break;

                    case AttachmentControl.RotateUp:
                        rotation.X += changeAmount * 10;
                        break;

                    case AttachmentControl.RotateDown:
                        rotation.X -= changeAmount * 10;
                        break;

                    case AttachmentControl.Confirm:
                        SetVehicleAsBeingUsed(_tempTowVehicle, false);
                        SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);

                        if (_attachmentStage == AttachmentStage.Position)
                        {
                            Screen.ShowNotification("~g~Remorquage réussi ! Conduisez prudemment.");

                            _tempVehicleBeingTowed.ResetOpacity();
                            _tempVehicleBeingTowed.IsCollisionEnabled = true;
                        }
                        else if (_attachmentStage == AttachmentStage.Detach)
                        {
                            Screen.ShowNotification($"Le véhicule : ~g~{_tempVehicleBeingTowed.LocalizedName ?? "Vehicle"} est détaché !");

                            ResetTowedVehicle(_tempVehicleBeingTowed);
                            SetVehicleAsBeingUsed(_tempVehicleBeingTowed, false);
                            RemoveTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed);
                        }

                        _tempTowVehicle = null;
                        _tempVehicleBeingTowed = null;

                        Game.PlaySound("WAYPOINT_SET", "HUD_FRONTEND_DEFAULT_SOUNDSET");

                        Tick -= AttachmentTick;
                        _attachmentStage = AttachmentStage.None;
                        return;
                }

                if (_tempTowVehicle.Position.DistanceToSquared(_tempTowVehicle.GetOffsetPosition(position)) > _config.MaxDistanceFromTowVehicle)
                {
                    Screen.ShowNotification("~r~Impossible de se déplacer jusqu'ici, trop loin du véhicule remorqueur !", true);
                }
                else
                {
                    _tempVehicleBeingTowed.AttachTo(_tempTowVehicle, position, rotation);

                    TowedVehicle updatedTowedVehicle = new TowedVehicle()
                    {
                        NetworkId = towedVehicle.NetworkId,
                        AttachmentPosition = position,
                        AttachmentRotation = rotation
                    };

                    UpdateTowedVehicle(_tempTowVehicle, _tempVehicleBeingTowed, updatedTowedVehicle);
                }
            }
        }

        /// <summary>
        /// Determines if a <see cref="Vehicle"/> is already being used as a
        /// tow truck (mid placement), or a vehicle being towed
        /// </summary>
        /// <param name="vehicle"><see cref="Vehicle"/> to check</param>
        /// <returns></returns>
        internal bool IsAlreadyBeingUsed(Vehicle vehicle)
        {
            if (vehicle.State.Get("isBeingUsed") == null)
            {
                vehicle.State.Set("isBeingUsed", false, true);
            }

            return vehicle.State.Get("isBeingUsed");
        }

        /// <summary>
        /// Sets a <see cref="Vehicle"/> as in use
        /// </summary>
        /// <param name="vehicle"><see cref="Vehicle"/> to set</param>
        /// <param name="beingUsed"><see cref="bool"/> to set</param>
        internal void SetVehicleAsBeingUsed(Vehicle vehicle, bool beingUsed)
        {
            // Initializes if null
            bool _ = IsAlreadyBeingUsed(vehicle);

            vehicle.State.Set("isBeingUsed", beingUsed, true);
        }

        /// <summary>
        /// Returns a <see cref="List{TowedVehicle}"/> a <see cref="Vehicle"/> is towing
        /// </summary>
        /// <param name="vehicle"><see cref="Vehicle"/> to check</param>
        /// <returns></returns>
        internal List<TowedVehicle> GetTowedVehicles(Vehicle vehicle)
        {
            if (vehicle.State.Get("vehiclesBeingTowed") == null)
            {
                vehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(new List<TowedVehicle>()), true);
            }

            return JsonConvert.DeserializeObject<List<TowedVehicle>>(vehicle.State.Get("vehiclesBeingTowed"));
        }

        /// <summary>
        /// Adds a new <see cref="TowedVehicle"/> as a vehicle being towed
        /// </summary>
        /// <param name="towVehicle"><see cref="Vehicle"/> doing the towing</param>
        /// <param name="towedVehicle"><see cref="TowedVehicle"/> being towed</param>
        internal void AddNewTowedVehicle(Vehicle towVehicle, TowedVehicle towedVehicle)
        {
            List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

            towedVehicles.Add(towedVehicle);

            towVehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(towedVehicles), true);
        }

        /// <summary>
        /// Updates a <see cref="TowedVehicle"/> that is already being towed
        /// </summary>
        /// <param name="towVehicle"><see cref="Vehicle"/> doing the towing</param>
        /// <param name="towedVehicle"><see cref="Vehicle"/> being towed</param>
        /// <param name="updatedTowedVehicle">Updated <see cref="TowedVehicle"/> information</param>
        internal void UpdateTowedVehicle(Vehicle towVehicle, Vehicle towedVehicle, TowedVehicle updatedTowedVehicle)
        {
            List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

            towedVehicles.RemoveAll(i => i.NetworkId == towedVehicle.NetworkId);
            towedVehicles.Add(updatedTowedVehicle);

            towVehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(towedVehicles), true);
        }

        /// <summary>
        /// Removes a <see cref="Vehicle"/> as being towed
        /// </summary>
        /// <param name="towVehicle"><see cref="Vehicle"/> doing the towing</param>
        /// <param name="towedVehicle"><see cref="Vehicle"/> being towed</param>
        internal void RemoveTowedVehicle(Vehicle towVehicle, Vehicle towedVehicle)
        {
            List<TowedVehicle> towedVehicles = GetTowedVehicles(towVehicle);

            towedVehicles.RemoveAll(i => i.NetworkId == towedVehicle.NetworkId);

            towVehicle.State.Set("vehiclesBeingTowed", JsonConvert.SerializeObject(towedVehicles), true);
        }
        #endregion
    }
}
