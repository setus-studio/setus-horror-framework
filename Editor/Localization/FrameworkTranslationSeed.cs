using System;
using System.Collections.Generic;

namespace Setus.HorrorFramework.Editor.Localization
{
    public static class FrameworkTranslationSeed
    {
        private const string Seed = @"
ui.main.new-game|Trò chơi mới|ニューゲーム|새 게임|Nueva partida|新游戏|新遊戲
ui.main.continue|Tiếp tục|続ける|계속하기|Continuar|继续|繼續
ui.common.save-load|Lưu / Tải|セーブ / ロード|저장 / 불러오기|Guardar / Cargar|保存 / 加载|儲存 / 載入
ui.common.settings|Cài đặt|設定|설정|Configuración|设置|設定
ui.common.back|Quay lại|戻る|뒤로|Volver|返回|返回
ui.pause.title|Tạm dừng|一時停止|일시 정지|En pausa|已暂停|已暫停
ui.pause.resume|Tiếp tục|再開|계속|Reanudar|继续|繼續
ui.pause.main-menu|Menu chính|メインメニュー|메인 메뉴|Menú principal|主菜单|主選單
ui.settings.title|Trợ năng & Cài đặt|アクセシビリティと設定|접근성 및 설정|Accesibilidad y configuración|辅助功能与设置|輔助功能與設定
ui.phone.status.new|Tin nhắn mới|新着メッセージ|새 메시지|Mensaje nuevo|新消息|新訊息
ui.phone.status.read|Đã đọc|既読|읽음|Leído|已读|已讀
ui.phone.status.replied|Đã trả lời|返信済み|답장함|Respondido|已回复|已回覆
narrative.speaker.player|Bạn|あなた|당신|Tú|你|你
objective.m9.sample.objective.title|Mục tiêu mới|新しい目標|새 목표|Nuevo objetivo|新目标|新目標
objective.m9.sample.objective.description|Mô tả mục tiêu dành cho người chơi.|プレイヤー向けの目標を記述します。|플레이어에게 보여 줄 목표를 설명합니다.|Describe el objetivo para el jugador.|描述面向玩家的目标。|描述給玩家的目標。
objective.m6.objective.arrive.title|Đến hành lang được đánh dấu|指定された廊下へ向かう|표시된 복도로 이동|Llega al pasillo marcado|前往标记的走廊|前往標記的走廊
objective.m6.objective.arrive.description|Đi qua khu vực được đánh dấu.|指定された場所を通過する。|표시된 구역을 통과하세요.|Cruza la zona marcada.|穿过标记区域。|穿過標記區域。
objective.m6.objective.read-note.title|Đọc nhật ký bảo trì|整備記録を読む|정비 기록 읽기|Lee el registro de mantenimiento|阅读维护日志|閱讀維護日誌
objective.m6.objective.read-note.description|Tìm ghi chú gần cánh cửa.|ドアの近くにあるメモを探す。|문 근처의 메모를 찾으세요.|Encuentra la nota junto a la puerta.|找到门边的笔记。|找到門邊的筆記。
objective.m6.objective.read-phone.title|Kiểm tra điện thoại|電話を確認する|휴대폰 확인|Mira el teléfono|查看手机|查看手機
objective.m6.objective.read-phone.description|Đọc tin nhắn mới.|新着メッセージを読む。|새 메시지를 읽으세요.|Lee el mensaje nuevo.|阅读新消息。|閱讀新訊息。
objective.m6.objective.use-route.title|Đi theo lối đã mở|開いた経路を使う|열린 경로 이용|Usa la ruta despejada|使用已打通的路线|使用已打通的路線
objective.m6.objective.use-route.description|Lối đi qua hành lang đã được mở.|廊下への経路が開いた。|복도 경로가 열렸습니다.|La ruta del pasillo está abierta.|走廊路线现已开放。|走廊路線現已開放。
objective.objective.1c5bcc271d0848fd9a01975adc075d7e.title|Mục tiêu mới|新しい目標|새 목표|Nuevo objetivo|新目标|新目標
objective.objective.1c5bcc271d0848fd9a01975adc075d7e.description|Mô tả mục tiêu dành cho người chơi.|プレイヤー向けの目標を記述します。|플레이어에게 보여 줄 목표를 설명합니다.|Describe el objetivo para el jugador.|描述面向玩家的目标。|描述給玩家的目標。
phone.m6.message.return-call.sender|Số lạ|不明な番号|알 수 없는 번호|Número desconocido|未知号码|未知號碼
phone.m6.message.return-call.message|Cửa bảo trì đã mở. Đừng ở lại hành lang.|整備用のドアが開いた。廊下に留まるな。|정비 문이 열렸다. 복도에 머물지 마.|La puerta de mantenimiento está abierta. No te quedes en el pasillo.|维护门开了。不要留在走廊里。|維護門開了。不要留在走廊裡。
phone.m6.message.return-call.reply|Tôi đang đến.|今向かっている。|지금 가고 있어.|Voy para allá.|我正在过去。|我正在過去。
note.m6.note.maintenance-log.title|Nhật ký bảo trì|整備記録|정비 기록|Registro de mantenimiento|维护日志|維護日誌
note.m6.note.maintenance-log.body|Điện sẽ trở lại sau khi lối qua hành lang được mở.|廊下への経路が開くと電力が復旧する。|복도 경로가 열리면 전력이 복구된다.|La energía vuelve cuando se despeja la ruta del pasillo.|走廊路线打通后电力会恢复。|走廊路線打通後電力會恢復。
scare.m9.sample.scare.speaker|Không rõ|不明|알 수 없음|Desconocido|未知|未知
scare.m9.sample.scare.cue|Có thứ gì đó vừa di chuyển gần đây.|近くで何かが動いた。|근처에서 무언가 움직였습니다.|Algo se movió cerca.|附近有什么东西动了。|附近有東西動了。
scare.m7.scare.debug-power.speaker|Debug|デバッグ|디버그|Depuración|调试|偵錯
scare.m7.scare.debug-power.cue|Tín hiệu scare debug.|デバッグ用の恐怖演出。|디버그 공포 연출.|Señal de susto de depuración.|调试惊吓提示。|偵錯驚嚇提示。
scare.m7.scare.hallway-power.speaker|Hành lang|廊下|복도|Pasillo|走廊|走廊
scare.m7.scare.hallway-power.cue|Đèn phía trước vừa tắt.|前方の明かりが消えた。|앞쪽 불이 꺼졌습니다.|Las luces se apagaron más adelante.|前方的灯熄灭了。|前方的燈熄滅了。
interaction.phone.check|Kiểm tra điện thoại|電話を確認|휴대폰 확인|Mirar el teléfono|查看手机|查看手機
interaction.phone.read|Đọc tin nhắn|メッセージを読む|메시지 읽기|Leer mensaje|阅读消息|閱讀訊息
interaction.phone.reply|Trả lời|返信|답장|Responder|回复|回覆
interaction.phone.handled|Đã đọc tin nhắn|メッセージ既読|메시지 읽음|Mensaje leído|消息已读|訊息已讀
interaction.phone.unavailable|Không thể dùng điện thoại|電話は使用できない|휴대폰을 사용할 수 없음|Teléfono no disponible|手机不可用|手機不可用
interaction.note.read|Đọc ghi chú|メモを読む|메모 읽기|Leer nota|阅读笔记|閱讀筆記
interaction.note.read-again|Đọc lại|もう一度読む|다시 읽기|Leer de nuevo|再次阅读|再次閱讀
interaction.note.unavailable|Không thể đọc tài liệu|文書は使用できない|문서를 사용할 수 없음|Documento no disponible|文档不可用|文件不可用
ui.settings.rebind.status.missing-actions|Chưa gán Input Actions.|Input Actions が割り当てられていません。|Input Actions가 할당되지 않았습니다.|No se ha asignado Input Actions.|未分配 Input Actions。|未指派 Input Actions。
ui.settings.rebind.status.action-unavailable|Input action không khả dụng.|入力アクションは使用できません。|입력 액션을 사용할 수 없습니다.|La acción de entrada no está disponible.|输入操作不可用。|輸入操作不可用。
ui.settings.rebind.status.waiting|Nhấn một phím. Esc để hủy.|入力してください。Esc でキャンセル。|키를 누르세요. Esc로 취소합니다.|Pulsa un control. Esc cancela.|请按一个按键。Esc 取消。|請按一個按鍵。Esc 取消。
ui.settings.rebind.status.reset|Đã đặt lại phím.|キー設定をリセットしました。|키 설정을 초기화했습니다.|Controles restablecidos.|按键已重置。|按鍵已重設。
ui.settings.rebind.status.updated|Đã cập nhật phím.|キー設定を更新しました。|키 설정을 업데이트했습니다.|Control actualizado.|按键已更新。|按鍵已更新。
ui.settings.rebind.status.cancelled|Đã hủy gán phím.|キー設定をキャンセルしました。|키 설정을 취소했습니다.|Cambio cancelado.|已取消改键。|已取消改鍵。
interaction.phone.result.not-configured|Tin nhắn điện thoại chưa được cấu hình.|電話メッセージが設定されていません。|휴대폰 메시지가 구성되지 않았습니다.|El mensaje del teléfono no está configurado.|手机消息未配置。|手機訊息未設定。
interaction.phone.result.delivered|Đã gửi tin nhắn|メッセージを受信した|메시지 전달됨|Mensaje entregado|消息已送达|訊息已送達
interaction.phone.result.delivery-failed|Gửi tin nhắn thất bại|メッセージの受信に失敗した|메시지 전달 실패|No se pudo entregar el mensaje|消息发送失败|訊息傳送失敗
interaction.phone.result.read|Đã đọc tin nhắn|メッセージを読んだ|메시지 읽음|Mensaje leído|消息已读|訊息已讀
interaction.phone.result.reply-sent|Đã gửi trả lời|返信を送信した|답장 전송됨|Respuesta enviada|回复已发送|回覆已傳送
interaction.phone.result.already-handled|Tin nhắn đã được xử lý|メッセージは処理済み|메시지가 이미 처리됨|El mensaje ya se gestionó|消息已处理|訊息已處理
interaction.note.result.not-configured|Ghi chú chưa được cấu hình.|メモが設定されていません。|메모가 구성되지 않았습니다.|La nota no está configurada.|笔记未配置。|筆記未設定。
interaction.note.result.read|Đã đọc ghi chú|メモを読んだ|메모 읽음|Nota leída|笔记已读|筆記已讀
ui.settings.master-volume|Âm lượng tổng|マスター音量|전체 음량|Volumen general|主音量|主音量
ui.settings.ambience-volume|Âm lượng môi trường|環境音|환경음 음량|Volumen de ambiente|环境音量|環境音量
ui.settings.sfx-volume|Âm lượng hiệu ứng|効果音|효과음 음량|Volumen de efectos|音效音量|音效音量
ui.settings.ui-volume|Âm lượng giao diện|UI 音量|UI 음량|Volumen de interfaz|界面音量|介面音量
ui.settings.voice-volume|Âm lượng giọng nói|ボイス音量|음성 음량|Volumen de voz|语音音量|語音音量
ui.settings.subtitles|Phụ đề|字幕|자막|Subtítulos|字幕|字幕
ui.settings.brightness|Độ sáng|明るさ|밝기|Brillo|亮度|亮度
ui.settings.camera-shake|Rung camera|カメラの揺れ|카메라 흔들림|Movimiento de cámara|镜头抖动|鏡頭晃動
ui.settings.head-bob|Nhấp nhô góc nhìn|ヘッドボブ|헤드 보빙|Balanceo de cámara|视角晃动|視角晃動
ui.settings.head-bob-intensity|Cường độ nhấp nhô|ヘッドボブの強さ|헤드 보빙 강도|Intensidad de balanceo|视角晃动强度|視角晃動強度
ui.settings.sprint-mode|Chạy nhanh dạng bật/tắt (tắt = giữ)|走る：切替（オフ＝長押し）|달리기 토글 (끔 = 길게 누르기)|Correr con alternancia (desactivado = mantener)|冲刺切换（关闭 = 按住）|衝刺切換（關閉 = 按住）
ui.settings.mouse-sensitivity|Độ nhạy chuột|マウス感度|마우스 감도|Sensibilidad del ratón|鼠标灵敏度|滑鼠靈敏度
ui.settings.locale-code|Ngôn ngữ|言語|언어|Idioma|语言|語言
ui.settings.input-remapping|Gán lại phím|キー設定|키 재설정|Asignación de controles|按键设置|按鍵設定
ui.settings.rebind.move-forward|Đi tới|前進|앞으로 이동|Avanzar|前进|前進
ui.settings.rebind.move-backward|Đi lùi|後退|뒤로 이동|Retroceder|后退|後退
ui.settings.rebind.move-left|Đi trái|左へ移動|왼쪽 이동|Mover a la izquierda|向左移动|向左移動
ui.settings.rebind.move-right|Đi phải|右へ移動|오른쪽 이동|Mover a la derecha|向右移动|向右移動
ui.settings.rebind.look|Nhìn|視点移動|시점|Mirar|视角|視角
ui.settings.rebind.sprint|Chạy nhanh|走る|달리기|Correr|冲刺|衝刺
ui.settings.rebind.crouch|Cúi người|しゃがむ|웅크리기|Agacharse|蹲下|蹲下
ui.settings.rebind.reset|Đặt lại phím|キー設定をリセット|키 설정 초기화|Restablecer controles|重置按键|重設按鍵
interaction.common.interact|Tương tác|調べる|상호작용|Interactuar|互动|互動
interaction.common.unavailable|Không thể tương tác|操作できない|상호작용할 수 없음|No se puede interactuar|无法互动|無法互動
interaction.door.open|Mở|開ける|열기|Abrir|打开|打開
interaction.door.close|Đóng|閉める|닫기|Cerrar|关闭|關閉
interaction.door.locked|Đã khóa|鍵がかかっている|잠김|Cerrado|已上锁|已上鎖
interaction.drawer.open|Mở ngăn kéo|引き出しを開ける|서랍 열기|Abrir cajón|打开抽屉|打開抽屜
interaction.drawer.close|Đóng ngăn kéo|引き出しを閉める|서랍 닫기|Cerrar cajón|关闭抽屉|關閉抽屜
interaction.drawer.locked|Ngăn kéo đã khóa|引き出しには鍵がかかっている|서랍이 잠겼습니다|El cajón está cerrado|抽屉已上锁|抽屜已上鎖
interaction.pickup.collect|Nhặt|拾う|줍기|Recoger|拾取|拾取
interaction.pickup.already-collected|Đã nhặt|取得済み|이미 주웠습니다|Ya recogido|已经拾取|已經拾取
interaction.inspectable.inspect|Kiểm tra|調べる|살펴보기|Examinar|检查|檢查
interaction.inspectable.already-inspected|Đã kiểm tra|確認済み|이미 살펴봤습니다|Ya examinado|已经检查|已經檢查
narrative.speaker.unknown|Không rõ|不明|알 수 없음|Desconocido|未知|未知
ai.stalker.feedback.chase|Có thứ gì đó đã phát hiện ra bạn.|何かに気づかれた。|무언가가 당신을 발견했습니다.|Algo te ha visto.|有什么东西发现了你。|有東西發現了你。
ui.save.error.no-save|Không có bản lưu.|セーブデータがありません。|저장된 게임이 없습니다.|No hay ninguna partida guardada.|没有可用的存档。|沒有可用的存檔。
ui.save.error.corrupt|Dữ liệu lưu bị hỏng và không thể tải.|セーブデータが破損しているためロードできません。|저장 데이터가 손상되어 불러올 수 없습니다.|Los datos guardados están dañados y no se pueden cargar.|存档已损坏，无法加载。|存檔已損壞，無法載入。
ui.save.error.incompatible|Bản lưu được tạo bởi phiên bản game không tương thích.|互換性のないバージョンで作成されたセーブです。|호환되지 않는 게임 버전에서 만든 저장 파일입니다.|Esta partida pertenece a una versión incompatible.|此存档由不兼容的游戏版本创建。|此存檔由不相容的遊戲版本建立。
ui.save.error.restore-failed|Không thể khôi phục bản lưu.|セーブを復元できませんでした。|저장된 게임을 복원할 수 없습니다.|No se pudo restaurar la partida.|无法恢复存档。|無法還原存檔。
ui.save.error.save-failed|Không thể lưu game.|セーブできませんでした。|게임을 저장할 수 없습니다.|No se pudo guardar la partida.|无法保存游戏。|無法儲存遊戲。
ui.save.error.unavailable|Tính năng lưu và tải hiện không khả dụng.|現在セーブとロードは使用できません。|현재 저장 및 불러오기를 사용할 수 없습니다.|Guardar y cargar no está disponible en este momento.|保存和加载当前不可用。|儲存與載入目前不可用。
ui.save.status.ready|Đã có bản lưu.|セーブデータがあります。|저장된 게임이 준비되었습니다.|Hay una partida guardada disponible.|存档已就绪。|存檔已就緒。
ui.save.status.saved|Đã lưu game.|セーブしました。|게임을 저장했습니다.|Partida guardada.|游戏已保存。|遊戲已儲存。
ui.save.status.loading|Đang tải bản lưu.|セーブをロードしています。|저장된 게임을 불러오는 중입니다.|Cargando partida.|正在加载存档。|正在載入存檔。
ui.save.error.outside-gameplay|Chỉ có thể Lưu nhanh trong gameplay.|ゲームプレイ中のみクイックセーブできます。|게임 플레이 중에만 빠른 저장을 사용할 수 있습니다.|El guardado rápido solo está disponible durante la partida.|快速保存仅在游戏中可用。|快速儲存僅能在遊戲中使用。
ui.load.error.from-pause|Không thể Tải nhanh từ menu tạm dừng.|ポーズメニューからクイックロードは使用できません。|일시 정지 메뉴에서는 빠른 불러오기를 사용할 수 없습니다.|La carga rápida no está disponible desde el menú de pausa.|暂停菜单中无法快速加载。|暫停選單中無法快速載入。
ui.settings.tab.audio|Âm thanh|オーディオ|오디오|Audio|音频|音訊
ui.settings.tab.controls|Điều khiển|操作|조작|Controles|控制|控制
ui.settings.tab.accessibility|Trợ năng|アクセシビリティ|접근성|Accesibilidad|辅助功能|輔助功能
ui.settings.tab.language|Ngôn ngữ|言語|언어|Idioma|语言|語言
ui.settings.tab.display|Hiển thị|画面|디스플레이|Pantalla|显示|顯示
ui.settings.reset.audio|Đặt lại âm thanh|オーディオを初期化|오디오 기본값 복원|Restablecer audio|重置音频|重設音訊
ui.settings.reset.movement|Đặt lại di chuyển|移動設定を初期化|이동 설정 초기화|Restablecer movimiento|重置移动设置|重設移動設定
ui.settings.reset.accessibility|Đặt lại trợ năng|アクセシビリティを初期化|접근성 기본값 복원|Restablecer accesibilidad|重置辅助功能|重設輔助功能
ui.settings.rebind.interact|Tương tác|調べる|상호작용|Interactuar|互动|互動
ui.settings.rebind.pause|Tạm dừng|ポーズ|일시 정지|Pausa|暂停|暫停
ui.settings.rebind.status.modal-unavailable|Chưa cấu hình hộp thoại gán phím.|キー設定ダイアログが未設定です。|키 설정 창이 구성되지 않았습니다.|El diálogo de asignación no está configurado.|改键对话框未配置。|改鍵對話框未設定。
ui.settings.rebind.status.conflict|Đã được gán cho|割り当て済み：|이미 할당됨:|Ya asignado a|已分配给|已指派給
ui.settings.rebind.dialog.reset-title|Đặt lại toàn bộ phím?|すべてのキーをリセットしますか？|모든 키를 초기화할까요?|¿Restablecer todos los controles?|重置所有按键？|重設所有按鍵？
ui.settings.rebind.dialog.reset-prompt|Điều khiển mặc định sẽ được khôi phục.|既定の操作に戻します。|기본 조작으로 복원됩니다.|Se restaurarán los controles predeterminados.|将恢复默认控制。|將還原預設控制。
ui.settings.rebind.dialog.cancel|Hủy|キャンセル|취소|Cancelar|取消|取消
ui.settings.rebind.dialog.retry|Thử lại|再試行|다시 시도|Reintentar|重试|重試
ui.settings.rebind.dialog.confirm-reset|Đặt lại|リセット|초기화|Restablecer|重置|重設
ui.settings.display.mode|Chế độ hiển thị|表示モード|화면 모드|Modo de pantalla|显示模式|顯示模式
ui.settings.display.resolution|Độ phân giải|解像度|해상도|Resolución|分辨率|解析度
ui.settings.display.refresh|Tần số quét|リフレッシュレート|화면 주사율|Frecuencia de actualización|刷新率|更新率
ui.settings.display.vsync|Đồng bộ dọc|垂直同期|수직 동기화|Sincronización vertical|垂直同步|垂直同步
ui.settings.display.frame-rate|Giới hạn FPS|FPS 上限|FPS 제한|Límite de FPS|FPS 上限|FPS 上限
ui.settings.display.quality|Chất lượng đồ họa|画質|그래픽 품질|Calidad gráfica|画质|畫質
ui.settings.display.quality.low|Thấp|低|낮음|Baja|低|低
ui.settings.display.quality.medium|Trung bình|中|중간|Media|中|中
ui.settings.display.quality.high|Cao|高|높음|Alta|高|高
ui.settings.display.anti-aliasing|Khử răng cưa|アンチエイリアス|안티앨리어싱|Suavizado de bordes|抗锯齿|反鋸齒
ui.settings.display.anti-aliasing.off|Tắt|オフ|끔|Desactivado|关闭|關閉
ui.settings.display.apply|Áp dụng hiển thị|画面設定を適用|화면 설정 적용|Aplicar pantalla|应用显示设置|套用顯示設定
ui.settings.display.keep|Giữ|維持|유지|Mantener|保留|保留
ui.settings.display.revert|Khôi phục|元に戻す|되돌리기|Revertir|还原|還原
ui.settings.display.mode.windowed|Cửa sổ|ウィンドウ|창 모드|Ventana|窗口|視窗
ui.settings.display.mode.borderless|Không viền|ボーダーレス|테두리 없음|Sin bordes|无边框|無邊框
ui.settings.display.mode.fullscreen|Toàn màn hình|フルスクリーン|전체 화면|Pantalla completa|全屏|全螢幕
ui.settings.display.automatic|Tự động|自動|자동|Automático|自动|自動
ui.settings.display.unlimited|Không giới hạn|無制限|무제한|Sin límite|无限制|無限制
ui.settings.display.confirm-prompt|Giữ cài đặt hiển thị này?|この画面設定を維持しますか？|이 화면 설정을 유지할까요?|¿Mantener estos ajustes de pantalla?|保留这些显示设置？|保留這些顯示設定？
ui.settings.display.status.previewing|Đang chờ xác nhận thay đổi hiển thị.|画面変更の確認待ちです。|화면 변경 확인을 기다리는 중입니다.|El cambio de pantalla espera confirmación.|显示更改正在等待确认。|顯示變更正在等待確認。
ui.settings.display.status.saved|Đã lưu cài đặt hiển thị.|画面設定を保存しました。|화면 설정을 저장했습니다.|Ajustes de pantalla guardados.|显示设置已保存。|顯示設定已儲存。
ui.settings.display.status.save-failed|Đã đổi hiển thị nhưng không thể lưu tùy chọn.|画面は変更されましたが設定を保存できませんでした。|화면은 변경되었지만 설정을 저장하지 못했습니다.|La pantalla cambió, pero no se pudieron guardar las preferencias.|显示已更改，但无法保存偏好设置。|顯示已變更，但無法儲存偏好設定。
ui.settings.display.status.apply-failed|Không thể đổi hiển thị; đã khôi phục cài đặt trước.|画面変更に失敗し、以前の設定に戻しました。|화면 변경에 실패하여 이전 설정을 복원했습니다.|Falló el cambio; se restauraron los ajustes anteriores.|显示更改失败，已恢复之前的设置。|顯示變更失敗，已還原先前設定。
ui.settings.display.status.timed-out|Hết thời gian xác nhận; đã khôi phục cài đặt trước.|確認時間切れのため以前の設定に戻しました。|확인 시간이 만료되어 이전 설정을 복원했습니다.|Se agotó el tiempo; se restauraron los ajustes anteriores.|确认超时，已恢复之前的设置。|確認逾時，已還原先前設定。
ui.settings.display.status.reverted|Đã khôi phục cài đặt hiển thị trước.|以前の画面設定に戻しました。|이전 화면 설정을 복원했습니다.|Se restauraron los ajustes de pantalla anteriores.|已恢复之前的显示设置。|已還原先前的顯示設定。
ui.settings.display.status.revert-failed|Không thể khôi phục hiển thị. Hãy kiểm tra cài đặt màn hình.|画面を復元できません。ディスプレイ設定を確認してください。|화면을 복원하지 못했습니다. 디스플레이 설정을 확인하세요.|No se pudo restaurar la pantalla. Revisa sus ajustes.|无法恢复显示。请检查屏幕设置。|無法還原顯示。請檢查螢幕設定。
";

        private static readonly Dictionary<string, string[]> Rows = Parse();

        public static string Get(string localeCode, string key, string englishFallback)
        {
            if (string.Equals(localeCode, "en", StringComparison.OrdinalIgnoreCase) ||
                !Rows.TryGetValue(key, out var translations))
            {
                return englishFallback ?? string.Empty;
            }

            var index = localeCode switch
            {
                "vi" => 0,
                "ja" => 1,
                "ko" => 2,
                "es" => 3,
                "zh-Hans" => 4,
                "zh-Hant" => 5,
                _ => -1
            };
            return index >= 0 && index < translations.Length && !string.IsNullOrWhiteSpace(translations[index])
                ? translations[index]
                : englishFallback ?? string.Empty;
        }

        private static Dictionary<string, string[]> Parse()
        {
            var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var rawLine in Seed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = rawLine.Split('|');
                if (parts.Length == 7)
                {
                    var values = new string[6];
                    Array.Copy(parts, 1, values, 0, values.Length);
                    rows[parts[0]] = values;
                }
            }

            return rows;
        }
    }
}
