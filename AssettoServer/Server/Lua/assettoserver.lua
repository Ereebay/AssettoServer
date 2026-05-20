local license = [[
Copyright (C)  2023 Niewiarowski, compujuckel

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.


Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with the Steamworks SDK by Valve Corporation, containing parts covered by the terms of the Steamworks SDK License, the licensors of this Program grant you additional permission to convey the resulting work.

Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with plugins published on https://www.patreon.com/assettoserver, the licensors of this Program grant you additional permission to convey the resulting work. 
]]

local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP()
local configUrl = baseUrl .. "/api/configuration"
local logoUrl = baseUrl .. "/assets/logo_42.png"
local srpLogoUrl = baseUrl .. "/assets/srp-logo-new.png?3"
local configurationLoading = false
local configuration
local authHeaders = {}

local function getConfiguration()
    web.get(configUrl, authHeaders, function (err, response)
        if response.status == 200 then
            ac.log("config loaded")
            configuration = stringify.parse(response.body)
            configurationLoading = false
        end
    end)
end

local function setValue(key, value)
    web.post(configUrl .. "?key=" .. key .."&value=" .. tostring(value), authHeaders, function (err, response)
        ac.debug("err", err)
        ac.debug("response", stringify(response))

        if response.status ~= 200 then
            ui.toast(ui.Icons.Ban, "更新 " .. key .. " 失败（" .. response.status .. "）")
        else
            local parsed = stringify.parse(response.body)
            if parsed.Status ~= "OK" then
                ui.toast(ui.Icons.Ban, "更新 " .. key .. " 失败（" .. parsed.ErrorMessage .. "）")
            else
                ui.toast(ui.Icons.Confirm, key .. " 已设置为 " .. tostring(value))
            end
        end
    end)
end

local apiKeyEvent = ac.OnlineEvent({
    ac.StructItem.key("AS_ApiKey"),
    key = ac.StructItem.string(32)
}, function (sender, message)
    if sender ~= nil then return end
    ac.debug("key", message.key)
    authHeaders["X-Car-Id"] = car.sessionID
    authHeaders["X-Api-Key"] = message.key
end)

apiKeyEvent({ key = "" })

local logoSize = vec2(68, 42)
local srpLogoSize = vec2(244, 64)
local isSRP = ac.getTrackID():find("^shuto_revival_project_beta") ~= nil

-- ui.textHyperlink not supported on <0.1.79
local function ui_hyperlink(link)
    if ui.textHyperlink == nil then
        ui.text(link)
    else
        if ui.textHyperlink(link) then
            os.openURL(link)
        end
    end
end

local function tab_About()
    ui.childWindow("license", ui.availableSpace(), function ()
        ui.offsetCursorY(10)
        ui.image(logoUrl, logoSize)
        ui.sameLine()
        ui.offsetCursorY(-15)
        ui.pushFont(ui.Font.Huge)
        ui.text("AssettoServer")
        ui.popFont()

        ui.textWrapped("本服务器运行于 AssettoServer，使得 Assetto Corsa 能够拥有在线交通流。AssettoServer 是自由软件，你也可以自己搭建一个交通服务器。")
        ui.text("")
        ui.textWrapped("访问官方网站了解更多：")
        ui.sameLine()
        ui_hyperlink("https://assettoserver.org")

        ui.textWrapped("官方 Discord 服务器：")
        ui.sameLine()
        ui_hyperlink("https://discord.gg/uXEXRcSkyz")

        ui.text("")
        ui.pushFont(ui.Font.Title)
        ui.textWrapped("支持 AssettoServer 开发")
        ui.popFont()
        ui.textWrapped("Patreon：")
        ui.sameLine()
        ui_hyperlink("https://patreon.com/assettoserver")

        if isSRP then
            ui.offsetCursorY(10)
            ui.image(srpLogoUrl, srpLogoSize)

            ui.offsetCursorY(5)
            ui.textWrapped("本服务器使用 Shutoko Revival Project 赛道。")
            ui.textWrapped("该项目致力于打造首都高速道路（又称湾岸，Shutoko）的权威版本，仅适用于 Assetto Corsa。")
            ui.text("")
            ui.textWrapped("官方 Discord 服务器：")
            ui.sameLine()
            ui_hyperlink("https://discord.gg/shutokorevivalproject")

            ui.text("")
            ui.pushFont(ui.Font.Title)
            ui.textWrapped("支持 Shutoko Revival Project 开发")
            ui.popFont()
            ui.textWrapped("Patreon：")
            ui.sameLine()
            ui_hyperlink("https://www.patreon.com/Shutoko_Revival_Project")
        end
    end)
end

local function tab_License()
    ui.childWindow("license", ui.availableSpace(), function ()
        ui.textWrapped(license)
    end)
end

local function ui_configObject(name, obj)
    if obj == nil then return end

    for i, value in ipairs(obj.Properties) do
        if value.Type == "object" then
            ui.treeNode(value.Name, nil, function () ui_configObject(name .. "." .. value.Name, value.Value) end)
        elseif value.Type == "list" then
            ui.treeNode(value.Name, nil, function ()
                for j, listItem in ipairs(value.Value) do
                    if value.EntryType == "object" then
                        ui.treeNode(j - 1, nil, function () ui_configObject(name .. "." .. value.Name .. "." .. j - 1, listItem) end)
                    else
                        ui.text(listItem)
                    end
                end
            end)
        elseif value.Type == "dict" then
            ui.treeNode(value.Name, nil, function ()
                for key, listItem in pairs(value.Value) do
                    if value.EntryType == "object" then
                        ui.treeNode(key, nil, function () ui_configObject(name .. "." .. value.Name .. "." .. key, listItem) end)
                    else
                        ui.treeNode(key, nil, function () ui.text(listItem) end)
                    end
                end
            end)
        else
            ui.beginGroup()
            if value.ReadOnly then ui.pushStyleColor(ui.StyleColor.Text, rgbm.new("#999")) end
            ui.textAligned(value.Name, nil, vec2(150, 0))
            if value.ReadOnly then ui.popStyleColor() end
            ui.endGroup()
            if value.Description ~= nil and ui.itemHovered() then
                ui.setTooltip(value.Description)
            end
            ui.sameLine()

            local id = name .. "." .. value.Name

            if value.Type == "System.Boolean" then
                if ui.checkbox("###" .. id, value.Value) and not value.ReadOnly then
                    value.Value = not value.Value
                end
            elseif value.Type == "enum" then
                ui.setNextItemWidth(ui.availableSpaceX() - 42)
                if ui.beginCombo("###" .. id, value.Value) then
                    for j, enumValue in ipairs(value.ValidValues) do
                        if ui.selectable(enumValue, enumValue == value.Value) then
                            value.Value = enumValue
                        end
                    end
                    ui.endCombo()
                end
            else
                local flags = ui.InputTextFlags.None
                if value.ReadOnly then
                    flags = ui.InputTextFlags.ReadOnly
                end
                ui.setNextItemWidth(ui.availableSpaceX() - 42)
                value.Value = ui.inputText("###" .. id, value.Value, flags)
            end
            ui.sameLine()

            if ui.button("###btn.".. id, vec2(30, 0), value.ReadOnly and ui.ButtonFlags.Disabled or 0) then
                ac.debug("lastId", id)
                ac.debug("lastValue", value.Value)
                setValue(id, value.Value)
            end
            ui.addIcon(ui.Icons.Save, vec2(16,16))
        end
    end
end

local function tab_Configuration()
    ui.textWrapped("此功能为实验性！修改的值在服务器重启后不会保留。")
    ui.childWindow("configuration", ui.availableSpace(), function ()
        ui_configObject("Root", configuration)
    end)
end

local function window_AssettoServer()
    ui.tabBar("main_tabBar", function ()
        ui.tabItem("关于", tab_About)
        ui.tabItem("许可证", tab_License)
        if sim.isAdmin then
            if configuration == nil and not configurationLoading then
                configurationLoading = true
                getConfiguration()
            end
            ui.tabItem("配置", tab_Configuration)
        end
    end)
end

ui.registerOnlineExtra(ui.Icons.Info, "AssettoServer", function () return true end, window_AssettoServer, nil, ui.OnlineExtraFlags.Tool)
