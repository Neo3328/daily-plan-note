import AppKit
import SwiftUI
import ServiceManagement

struct Entry: Codable, Identifiable {
    var id = UUID()
    var text: String
    var done = false
}

struct WindowAccess: NSViewRepresentable {
    let pinned: Bool
    func makeNSView(context: Context) -> NSView { NSView() }
    func updateNSView(_ view: NSView, context: Context) {
        DispatchQueue.main.async {
            guard let window = view.window else { return }
            window.level = pinned ? .floating : .normal
            if window.frameAutosaveName.isEmpty { window.setFrameAutosaveName("DailyPlanWindow") }
        }
    }
}

struct Planner: View {
    @AppStorage("plans") private var data = Data()
    @AppStorage("pinned") private var pinned = false
    @AppStorage("language") private var language = "zh"
    @AppStorage("appearance") private var appearance = "system"
    @State private var date = Date()
    @State private var draft = ""
    @State private var message = ""
    @State private var settings = false
    @State private var loginEnabled = SMAppService.mainApp.status == .enabled
    private var en: Bool { language == "en" }
    private var key: String {
        let c = Calendar(identifier: .gregorian).dateComponents([.year,.month,.day], from: date)
        return String(format:"%04d-%02d-%02d", c.year!,c.month!,c.day!)
    }
    private var days: [String:[Entry]] { (try? JSONDecoder().decode([String:[Entry]].self,from:data)) ?? [:] }
    private var entries: [Entry] { days[key] ?? [] }
    private func edit(_ change: (inout [Entry])->Void) {
        do {
            var all = data.isEmpty ? [:] : try JSONDecoder().decode([String:[Entry]].self,from:data)
            change(&all[key,default:[]])
            data = try JSONEncoder().encode(all)
        } catch { message = error.localizedDescription }
    }
    private func add() {
        let text = draft.trimmingCharacters(in:.whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        edit { $0.append(Entry(text:text)) }; draft = ""
    }
    var body: some View {
        VStack(alignment:.leading, spacing:14) {
            HStack {
                Text(en ? "Daily Plan" : "每日计划").font(.title.bold())
                Text("\(entries.filter{$0.done}.count) / \(entries.count)").foregroundStyle(.secondary)
                Spacer()
                Button { settings.toggle() } label: { Image(systemName:"gearshape") }
                    .popover(isPresented:$settings) {
                        VStack(alignment:.leading) {
                            Picker(en ? "Language" : "语言",selection:$language) {
                                Text("简体中文").tag("zh"); Text("English").tag("en")
                            }
                            Picker(en ? "Appearance" : "外观",selection:$appearance) {
                                Text(en ? "System" : "跟随系统").tag("system")
                                Text(en ? "Light" : "浅色").tag("light")
                                Text(en ? "Dark" : "深色").tag("dark")
                            }
                            Toggle(en ? "Open at login" : "登录时打开",isOn:Binding(get:{loginEnabled},set:{ enabled in
                                do {
                                    if enabled { try SMAppService.mainApp.register() }
                                    else { try SMAppService.mainApp.unregister() }
                                    if SMAppService.mainApp.status == .requiresApproval {
                                        SMAppService.openSystemSettingsLoginItems()
                                    }
                                } catch { message = error.localizedDescription }
                                loginEnabled = SMAppService.mainApp.status == .enabled
                            }))
                        }.padding().frame(width:240)
                    }
            }
            HStack {
                DatePicker(en ? "Date" : "日期",selection:$date,displayedComponents:.date).labelsHidden()
                Spacer()
                Button(en ? "Today" : "今天"){ date = Date() }
            }
            ProgressView(value:Double(entries.filter{$0.done}.count),total:Double(max(1,entries.count)))
            ScrollView {
                VStack(spacing:10) {
                    ForEach(entries){ item in
                        HStack {
                            Button { edit { list in
                                if let i=list.firstIndex(where:{$0.id==item.id}) { list[i].done.toggle() }
                            }} label:{ Image(systemName:item.done ? "checkmark.circle.fill":"circle") }
                            .buttonStyle(.plain)
                            TextField(en ? "Plan" : "计划",text:Binding(get:{entries.first(where:{$0.id==item.id})?.text ?? ""},set:{ text in
                                edit { list in if let i=list.firstIndex(where:{$0.id==item.id}) { list[i].text=text } }
                            })).textFieldStyle(.plain).strikethrough(item.done)
                            Button { edit { $0.removeAll(where:{$0.id==item.id}) } } label:{Image(systemName:"minus.circle")}
                                .buttonStyle(.plain).foregroundStyle(.secondary)
                        }
                    }
                }.padding(.vertical,4)
            }
            HStack {
                TextField(en ? "Add a plan" : "添加一条计划",text:$draft).onSubmit(add)
                Button(action:add){Image(systemName:"plus")}
            }
            Toggle(en ? "Keep on top" : "置顶",isOn:$pinned).toggleStyle(.switch)
        }
        .accentColor(.orange).padding(20).frame(minWidth:320,minHeight:320)
        .environment(\.locale,Locale(identifier:en ? "en_US":"zh_CN"))
        .preferredColorScheme(appearance=="dark" ? .dark : appearance=="light" ? .light : nil)
        .background(WindowAccess(pinned:pinned))
        .alert(en ? "Daily Plan" : "每日计划",isPresented:Binding(get:{!message.isEmpty},set:{if !$0 {message=""}})){
            Button("OK"){message=""}
        } message:{Text(message)}
    }
}

@main struct DailyPlanNote: App {
    var body: some Scene {
        Window("Daily Plan",id:"planner"){ Planner() }.defaultSize(width:380,height:440)
    }
}
