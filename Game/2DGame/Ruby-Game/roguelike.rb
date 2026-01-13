require 'curses'
require 'set'

# 区画を表すクラス
class Partition
  attr_accessor :x, :y, :width, :height

  def initialize(x, y, width, height)
    @x = x
    @y = y
    @width = width
    @height = height
  end

  def left_partition
    Partition.new(@x, @y, @width / 2, @height)
  end

  def right_partition
    Partition.new(@x + @width / 2, @y, @width - @width / 2, @height)
  end

  def top_partition
    Partition.new(@x, @y, @width, @height / 2)
  end

  def bottom_partition
    Partition.new(@x, @y + @height / 2, @width, @height - @height / 2)
  end
end

# 部屋を表すクラス
class Room
  attr_accessor :x, :y, :width, :height

  def initialize(x, y, width, height)
    @x = x
    @y = y
    @width = width
    @height = height
  end

  def center_x
    @x + @width / 2
  end

  def center_y
    @y + @height / 2
  end
end

# ダンジョン生成クラス
class DungeonGenerator
  attr_reader :rooms
  MIN_ROOM_SIZE = 5
  MAX_ROOM_SIZE = 15

  # size_mode: :uniform, :small, :large
  def initialize(width, height, rooms_count: 4, min_room_size: MIN_ROOM_SIZE, max_room_size: MAX_ROOM_SIZE, size_mode: :uniform)
    @width = width
    @height = height
    @rooms_count = rooms_count.clamp(2, 8)
    @min_room_size = [min_room_size, 5].max
    @max_room_size = [max_room_size, @min_room_size].max
    @size_mode = size_mode
    @map = Array.new(height) { Array.new(width, '#') }
    @rooms = []
  end

  # 4セクションに分割し、各セクションに部屋を生成（rooms_countで数を調整可能）
  def generate
    # 初期化：全て壁
    @map = Array.new(@height) { Array.new(@width, '#') }
    @rooms = []
    @corridors = Set.new  # 通路として描画されたマスを記録

    # セクションを4つ定義（左上, 右上, 左下, 右下）
    mid_x = @width / 2
    mid_y = @height / 2

    sections = []
    sections << { x: 0,       y: 0,       w: mid_x,         h: mid_y } # left-top
    sections << { x: mid_x,   y: 0,       w: @width - mid_x, h: mid_y } # right-top
    sections << { x: 0,       y: mid_y,   w: mid_x,         h: @height - mid_y } # left-bottom
    sections << { x: mid_x,   y: mid_y,   w: @width - mid_x, h: @height - mid_y } # right-bottom

    # rooms_count に応じてセクションを選択（rooms_count >=4 の場合は各セクションに1以上）
    chosen_sections = []
    if @rooms_count >= 4
      # まず各セクションに1つ割り当て、余りをランダムに振る
      chosen_sections = sections.dup
      extras = @rooms_count - 4
      extras.times { chosen_sections << sections.sample }
    else
      # rooms_count < 4 の場合は先頭の sections から選ぶ
      chosen_sections = sections.first(@rooms_count)
    end

    # 各選ばれたセクションに対して部屋を生成
    chosen_sections.each do |sec|
      # 部屋サイズはセクションより少し小さくする
      max_w = [sec[:w] - 2, @max_room_size].min
      max_h = [sec[:h] - 2, @max_room_size].min
      w_min = [@min_room_size, 3].min
      h_min = [@min_room_size, 3].min
      room_w = choose_size(w_min, max_w, @size_mode)
      room_h = choose_size(h_min, max_h, @size_mode)

      # 部屋の配置位置をセクション内のランダム位置に（1セルの余白を確保）
      x_min = sec[:x] + 1
      x_max = sec[:x] + sec[:w] - room_w - 1
      y_min = sec[:y] + 1
      y_max = sec[:y] + sec[:h] - room_h - 1

      room_x = x_min <= x_max ? rand(x_min..x_max) : x_min
      room_y = y_min <= y_max ? rand(y_min..y_max) : y_min

      room = Room.new(room_x, room_y, room_w, room_h)
      @rooms << room

      # マップに床を描画
      room_y.upto(room_y + room_h - 1) do |yy|
        room_x.upto(room_x + room_w - 1) do |xx|
          @map[yy][xx] = '.' if valid?(xx, yy)
        end
      end
    end

    # 部屋をツリー構造で繋ぐ（重複を避ける）
    # 最初の部屋を基準に、他の部屋を順に接続
    if @rooms.length > 1
      (0...@rooms.length - 1).each do |i|
        room = @rooms[i]
        next_room = @rooms[i + 1]
        create_corridor(room.center_x, room.center_y, next_room.center_x, next_room.center_y)
      end
    end

    # 文字列配列として返す
    @map.map { |row| row.join }
  end

  # 部屋サイズ選択ヘルパー（モードによる分布制御）
  def choose_size(min_sz, max_sz, mode)
    min_sz = [[min_sz, 3].max, max_sz].min
    max_sz = [max_sz, min_sz].max
    return min_sz if min_sz == max_sz

    case mode
    when :small
      # 小さい部屋が出やすい（rand^2）
      span = max_sz - min_sz + 1
      offset = (rand ** 2 * span).to_i
      [min_sz + offset, max_sz].min
    when :large
      # 大きい部屋が出やすい（1 - rand^2）
      span = max_sz - min_sz + 1
      offset = (1 - rand ** 2) * span
      [min_sz + offset.to_i, max_sz].min
    else
      rand(min_sz..max_sz)
    end
  end

  private

  def divide_partition(partition, depth)
    # 区画数が目標に達したら分割を終了
    return if @partitions.length >= 6

    # 最小サイズ以下なら分割しない
    return if partition.width < 20 || partition.height < 15

    @partitions << partition

    # ランダムに水平または垂直分割
    if rand < 0.5 && partition.width > partition.height
      left = partition.left_partition
      right = partition.right_partition
      divide_partition(left, depth + 1)
      divide_partition(right, depth + 1)
    elsif partition.height > partition.width
      top = partition.top_partition
      bottom = partition.bottom_partition
      divide_partition(top, depth + 1)
      divide_partition(bottom, depth + 1)
    else
      divide_partition(partition.left_partition, depth + 1) if rand < 0.5
      divide_partition(partition.right_partition, depth + 1)
    end
  end

  def create_room(partition)
    # 区画内にランダムサイズの部屋を生成
    room_width = [MIN_ROOM_SIZE, partition.width - 2].max
    room_width = rand(MIN_ROOM_SIZE..room_width)

    room_height = [MIN_ROOM_SIZE, partition.height - 2].max
    room_height = rand(MIN_ROOM_SIZE..room_height)

    # 部屋を区画内の中央付近に配置
    room_x = partition.x + (partition.width - room_width) / 2
    room_y = partition.y + (partition.height - room_height) / 2

    room = Room.new(room_x, room_y, room_width, room_height)
    @rooms << room

    # マップに部屋を描画（床を配置）
    room_y.upto(room_y + room_height - 1) do |y|
      room_x.upto(room_x + room_width - 1) do |x|
        @map[y][x] = '.'
      end
    end
  end

  def connect_rooms
    # 部屋を隣接順に繋ぐ
    @rooms.each_with_index do |room, i|
      next if i == @rooms.length - 1

      # 次の部屋と繋ぐ
      next_room = @rooms[i + 1]
      create_corridor(room.center_x, room.center_y, next_room.center_x, next_room.center_y)
    end
  end

  def create_corridor(x1, y1, x2, y2)
    # 水平線を引く
    x_start = [x1, x2].min
    x_end = [x1, x2].max
    x_start.upto(x_end) do |x|
      if valid?(x, y1) && !@corridors.include?([x, y1])
        @map[y1][x] = '.'
        @corridors.add([x, y1])
      end
    end

    # 垂直線を引く
    y_start = [y1, y2].min
    y_end = [y1, y2].max
    y_start.upto(y_end) do |y|
      if valid?(x2, y) && !@corridors.include?([x2, y])
        @map[y][x2] = '.'
        @corridors.add([x2, y])
      end
    end
  end

  def valid?(x, y)
    x >= 0 && x < @width && y >= 0 && y < @height
  end
end

# 基底クラス：すべてのゲームエンティティの基本
class Entity
  attr_accessor :x, :y, :max_hp, :current_hp

  def initialize(x, y, max_hp = 10)
    @x = x
    @y = y
    @max_hp = max_hp
    @current_hp = max_hp
  end

  def alive?
    @current_hp > 0
  end

  def take_damage(amount)
    @current_hp = [@current_hp - amount, 0].max
  end

  def draw(win)
    raise NotImplementedError, 'Subclass must implement draw method'
  end
end

class Player < Entity
  attr_reader :level, :exp, :exp_to_next, :atk, :arrows, :weapon_atk, :armor_defense, :weapon_name, :armor_name
  def initialize(x, y, max_hp = 10)
    super(x, y, max_hp)
    @level = 1
    @exp = 0
    @exp_to_next = 30
    @atk = 3
    @arrows = 0
    @weapon_atk = 0
    @armor_defense = 0
    @weapon_name = "なし"
    @armor_name = "なし"
  end

  def equip_weapon(atk, name = "武器")
    @weapon_atk = atk
    @weapon_name = name
  end

  def equip_armor(defense, name = "防具")
    @armor_defense = defense
    @armor_name = name
  end

  def total_atk
    @atk + @weapon_atk
  end

  def total_defense
    @armor_defense
  end

  # 経験値を加算し、レベルアップ判定を行う
  def add_exp(amount, message_log = nil)
    # deprecated: kept for backward compat, forward to gain_exp
    gain_exp(amount, message_log)
  end

  # 経験値を獲得してレベルアップ判定を行う
  def gain_exp(amount, message_log = nil)
    return if amount <= 0
    @exp += amount
    while @exp >= @exp_to_next
      @exp -= @exp_to_next
      level_up(message_log)
    end
  end

  def level_up(message_log = nil)
    @level += 1
    @max_hp += 5
    @current_hp = @max_hp
    @atk += 1
    # 次のレベルまでの必要経験値を1.5倍（整数）
    @exp_to_next = (@exp_to_next * 1.5).to_i
    message_log.add("レベルアップ！ レベル#{@level}になった！") if message_log
  end

  # 矢を獲得する
  def add_arrows(n)
    @arrows = (@arrows || 0) + (n || 0)
  end

  # 敵からのダメージに特化した処理（点滅効果を付与：0.5秒）
  def hurt(amount, source = :enemy)
    # 実際のHP減少は基底の take_damage を使う
    take_damage(amount)
    # 敵からの攻撃なら時刻ベースで点滅を開始（0.5秒）
    if source == :enemy && amount > 0
      @blink_until = Time.now + 0.25
    end 
  end

  # 毎ターンに呼び出される（将来の拡張用）
  def tick
    # 点滅は時刻ベースで管理しているためここでは何もしない
  end

  def move(dx, dy, max_x, max_y, map)
    nx = @x + dx
    ny = @y + dy

    # 境界チェック
    return false if nx < 0 || nx >= max_x || ny < 0 || ny >= max_y
    
    # 衝突判定（壁は'#'）
    return false if map[ny][nx] == '#'
    
    @x = nx
    @y = ny
    
    # 移動するたびにHPが1減少
    take_damage(1)
    
    return true
  end

  def draw(win)
    win.setpos(@y, @x)
    # 時刻ベースで点滅を判定（0.5秒）
    blinking = @blink_until && Time.now < @blink_until
    if blinking && Curses.respond_to?(:color_pair)
      begin
        if Curses.const_defined?(:A_BLINK)
          Curses.attron(Curses.color_pair(7) | Curses::A_BLINK)
          win.addch('@')
          Curses.attroff(Curses.color_pair(7) | Curses::A_BLINK)
        else
          Curses.attron(Curses.color_pair(7))
          win.addch('@')
          Curses.attroff(Curses.color_pair(7))
        end
      rescue
        # 属性が使えない場合は通常表示
        win.addch('@')
      end
    else
      win.addch('@')
    end
  end
end

# 敵クラス（Entity を継承）
class Enemy < Entity
  attr_accessor :name, :damage, :symbol, :color_pair

  TYPE_STATS = {
    'Slime'    => { hp: 5,  damage: 2,  symbol: 'S', color_pair: 1, weight: 40, exp: 10 },
    'Goblin'   => { hp: 15, damage: 4,  symbol: 'G', color_pair: 2, weight: 30, exp: 35 },
    'Bat'      => { hp: 10,  damage: 5,  symbol: 'B', color_pair: 3, weight: 20, exp: 25 },
    'Dragon'   => { hp: 30, damage: 15, symbol: 'D', color_pair: 4, weight: 10, exp: 300 },
    'Splitter' => { hp: 20,  damage: 7,  symbol: 'M', color_pair: 5, weight: 10, exp: 50 },
    'Ghost'    => { hp: 15, damage: 8, symbol: 'O', color_pair: 6, weight: 10, exp: 44 }
  }

  def initialize(x, y, type = 'Slime')
    stats = TYPE_STATS[type] || TYPE_STATS['Slime']
    super(x, y, stats[:hp])
    @name = type
    @damage = stats[:damage]
    @symbol = stats[:symbol]
    @color_pair = stats[:color_pair]
  end

  def exp_reward
    TYPE_STATS[@name] ? TYPE_STATS[@name][:exp] || 0 : 0
  end

  # プレイヤーに近づく簡易AI
  def move_towards(player_x, player_y, max_x, max_y, map, enemies = [])
    dx = 0
    dy = 0

    if player_x < @x
      dx = -1
    elsif player_x > @x
      dx = 1
    end

    if player_y < @y
      dy = -1
    elsif player_y > @y
      dy = 1
    end

    nx = @x + dx
    ny = @y + dy

    return false if nx < 0 || nx >= max_x || ny < 0 || ny >= max_y
    return false if map[ny][nx] == '#'
    return false if nx == player_x && ny == player_y
    # 敵同士が重ならないようにチェック
    return false if enemies.any? { |e| e != self && e.x == nx && e.y == ny }

    @x = nx
    @y = ny
    true
  end

  # ターンごとの行動（ここで特殊能力を処理）
  # player オブジェクトを受け取り、必要なら直接ダメージを与える
  def act(player, max_x, max_y, map, enemies = [], message_log = nil)
    case @name
    when 'Bat'
      # 30% の確率で2回移動（2マス移動相当）
      if rand < 0.3
        moved = move_towards(player.x, player.y, max_x, max_y, map, enemies)
        if moved
          # 2回目の移動はブロックされる可能性がある
          move_towards(player.x, player.y, max_x, max_y, map, enemies)
        end
        message_log.add("#{@name}が素早く移動した！") if message_log
        return nil
      else
        move_towards(player.x, player.y, max_x, max_y, map, enemies)
        return nil
      end

    when 'Goblin'
      # 直線上（同一行または同一列）かつ壁が間に無ければ弓で遠距離攻撃
      # ただし近接（距離==1）の場合は射撃しない（隣接攻撃は別途発生）
      player_x = player.x
      player_y = player.y
      if player_x == @x || player_y == @y
        # 距離計算（マンハッタン距離）
        dist = (player_x - @x).abs + (player_y - @y).abs
        if dist > 1 && dist <= 10
          blocked = false
          if player_x == @x
            # 同列チェック（y方向）
            range_y = (@y < player_y) ? ((@y + 1)...player_y) : ((player_y + 1)...@y)
            range_y.each do |yy|
              if map[yy][@x] == '#'
                blocked = true
                break
              end
            end
          else
            # 同行チェック（x方向）
            range_x = (@x < player_x) ? ((@x + 1)...player_x) : ((player_x + 1)...@x)
            range_x.each do |xx|
              if map[@y][xx] == '#'
                blocked = true
                break
              end
            end
          end

            unless blocked
            # 射撃成功：プレイヤーにダメージ
            damage = @damage
            message_log.add("#{@name}が弓を放った！") if message_log

            # 弾道座標（敵とプレイヤーの間）を収集（敵とプレイヤーのマスは除外）
            # ここで常に敵側（@x,@y）からプレイヤー方向へ向かう順序で座標を入れる
            cells = []
            if player_x == @x
              # 同列。敵からプレイヤーへ step で進める
              step = (player_y > @y) ? 1 : -1
              cy = @y + step
              while cy != player_y
                cells << [@x, cy]
                cy += step
              end
            else
              # 同行。敵からプレイヤーへ step で進める
              step = (player_x > @x) ? 1 : -1
              cx = @x + step
              while cx != player_x
                cells << [cx, @y]
                cx += step
              end
            end

            # 末尾にプレイヤーの座標を追加して "敵位置からプレイヤー位置まで" の軌跡にする
            cells << [player_x, player_y]

            # 通路に敵がいるか、壁があるかをチェック（間に誰かがいる場合は射撃しない）
            blocked = cells.any? { |cx, cy| map[cy][cx] == '#' || enemies.any? { |e| e != self && e.x == cx && e.y == cy } }
            unless blocked
              # act の呼び出し元（Game#update）に描画情報とダメージを渡す
              return { cells: cells, symbol: (player_x == @x ? '|' : '-'), damage: damage }
            end
          end
        end
      end
      # 条件に合わない場合は通常移動
      move_towards(player.x, player.y, max_x, max_y, map, enemies)
      return nil

    when 'Dragon'
      # 20% の確率でランダムに2マス移動
      if rand < 0.2
        dirs = [[1,0],[-1,0],[0,1],[0,-1]].shuffle
        moved = false
        dirs.each do |dx, dy|
          nx = @x + dx
          ny = @y + dy
          next unless nx.between?(0, max_x - 1) && ny.between?(0, max_y - 1)
          next if map[ny][nx] == '#'
          next if enemies.any? { |e| e != self && e.x == nx && e.y == ny }
          @x = nx
          @y = ny
          moved = true
          break
        end
        if moved
          # 2回目の移動も試みる（同じ方向が望ましいが、他方向でも可）
          dirs.each do |dx, dy|
            nx = @x + dx
            ny = @y + dy
            next unless nx.between?(0, max_x - 1) && ny.between?(0, max_y - 1)
            next if map[ny][nx] == '#'
            next if enemies.any? { |e| e != self && e.x == nx && e.y == ny }
            @x = nx
            @y = ny
            break
          end
          message_log.add("#{@name}が不規則に移動した！") if message_log
          return nil
        end
      end

      # 50% の確率で炎のブレス（Goblin の矢と同様の直線攻撃）
      player_x = player.x
      player_y = player.y
      if player_x == @x || player_y == @y
        dist = (player_x - @x).abs + (player_y - @y).abs
        if dist > 1 && dist <= 10 && rand < 0.5
          blocked = false
          cells = []
          if player_x == @x
            step = (player_y > @y) ? 1 : -1
            cy = @y + step
            while cy != player_y
              cells << [@x, cy]
              cy += step
            end
          else
            step = (player_x > @x) ? 1 : -1
            cx = @x + step
            while cx != player_x
              cells << [cx, @y]
              cx += step
            end
          end

          # 末尾はプレイヤー位置
          cells << [player_x, player_y]

          # 間に壁や他敵がいれば中断
          blocked = cells.any? { |cx, cy| map[cy][cx] == '#' || enemies.any? { |e| e != self && e.x == cx && e.y == cy } }
          unless blocked
            message_log.add("#{@name}が炎のブレスを吐いた！") if message_log
            return { cells: cells, symbol: (player_x == @x ? '!' : '~'), damage: @damage, color: 4 }
          end
        end
      end
      # 条件に合わない場合は通常移動
      move_towards(player.x, player.y, max_x, max_y, map, enemies)
      return nil

    when 'Ghost'
      # Ghost: wall-phasing, random move unless player within 4 tiles, then approach
      dist = (player.x - @x).abs + (player.y - @y).abs
      if dist <= 4
        # Move towards player, ignoring walls
        dx = player.x < @x ? -1 : (player.x > @x ? 1 : 0)
        dy = player.y < @y ? -1 : (player.y > @y ? 1 : 0)
        nx = @x + dx
        ny = @y + dy
        # Stay in bounds, avoid overlapping other enemies and player
        if nx >= 0 && nx < max_x && ny >= 0 && ny < max_y && enemies.none? { |e| e != self && e.x == nx && e.y == ny } && !(player.x == nx && player.y == ny)
          @x = nx
          @y = ny
        end
      else
        # Random move, ignoring walls
        dirs = [[1,0],[-1,0],[0,1],[0,-1]].shuffle
        dirs.each do |dx, dy|
          nx = @x + dx
          ny = @y + dy
          if nx >= 0 && nx < max_x && ny >= 0 && ny < max_y && enemies.none? { |e| e != self && e.x == nx && e.y == ny } && !(player.x == nx && player.y == ny)
            @x = nx
            @y = ny
            break
          end
        end
      end
      return nil

    else
      # デフォルトは1回移動
      move_towards(player.x, player.y, max_x, max_y, map, enemies)
      return nil
    end
  end

  # 点滅（被弾など）を開始する
  def flash(duration = 0.25)
    @blink_until = Time.now + duration
  end

  def blinking?
    @blink_until && Time.now < @blink_until
  end

  def draw(win)
    win.setpos(@y, @x)
    if blinking? && Curses.respond_to?(:color_pair)
      begin
        if Curses.const_defined?(:A_BLINK)
          Curses.attron(Curses.color_pair(@color_pair) | Curses::A_BLINK)
          win.addch(@symbol)
          Curses.attroff(Curses.color_pair(@color_pair) | Curses::A_BLINK)
        else
          # A_BLINK が無ければカラーだけで強調（太字）
          if Curses.const_defined?(:A_BOLD)
            Curses.attron(Curses.color_pair(@color_pair) | Curses::A_BOLD)
            win.addch(@symbol)
            Curses.attroff(Curses.color_pair(@color_pair) | Curses::A_BOLD)
          else
            Curses.attron(Curses.color_pair(@color_pair))
            win.addch(@symbol)
            Curses.attroff(Curses.color_pair(@color_pair))
          end
        end
      rescue
        win.addch(@symbol)
      end
    else
      if Curses.respond_to?(:color_pair)
        Curses.attron(Curses.color_pair(@color_pair))
        win.addch(@symbol)
        Curses.attroff(Curses.color_pair(@color_pair))
      else
        win.addch(@symbol)
      end
    end
  end
end

# メッセージログクラス
class MessageLog
  MAX_MESSAGES = 3

  def initialize
    @messages = []
  end

  def add(message)
    @messages.unshift(message)
    @messages.pop if @messages.size > MAX_MESSAGES
  end

  def draw(win, start_y)
    @messages.each_with_index do |msg, i|
      win.setpos(start_y + i, 0)
      win.addstr(msg[0..50])  # 最大50文字まで表示
    end
  end

  def clear
    @messages.clear
  end
end

# アイテム（回復薬など）
class Item
  attr_accessor :x, :y, :symbol, :heal_amount, :color_pair

  def initialize(x, y, heal_amount = 50)
    @x = x
    @y = y
    @heal_amount = heal_amount
    @symbol = 'P'
    @color_pair = 5  # 黄色
  end

  def draw(win)
    win.setpos(@y, @x)
    if Curses.respond_to?(:color_pair)
      Curses.attron(Curses.color_pair(@color_pair))
      win.addch(@symbol)
      Curses.attroff(Curses.color_pair(@color_pair))
    else
      win.addch(@symbol)
    end
  end
end

# 矢アイテム（拾うと矢を所持数に追加）
class ArrowItem < Item
  attr_accessor :count
  def initialize(x, y, count = 1)
    super(x, y, 0)
    @symbol = ')'
    @count = count
    @color_pair = 5  # 黄色
  end
end

# 階段
class Stairs
  attr_accessor :x, :y, :symbol, :color_pair

  def initialize(x, y)
    @x = x
    @y = y
    @symbol = '>'
    @color_pair = 6  # 水色
  end

  def draw(win)
    win.setpos(@y, @x)
    if Curses.respond_to?(:color_pair)
      Curses.attron(Curses.color_pair(@color_pair))
      win.addch(@symbol)
      Curses.attroff(Curses.color_pair(@color_pair))
    else
      win.addch(@symbol)
    end
  end
end

class WeaponItem
  attr_reader :x, :y, :atk, :name
  def initialize(x, y, atk, name = "武器")
    @x = x
    @y = y
    @atk = atk
    @name = name
  end
  def draw(win)
    win.setpos(@y, @x)
    win.addch('W')
  end
end

class ArmorItem
  attr_reader :x, :y, :defense, :name
  def initialize(x, y, defense, name = "防具")
    @x = x
    @y = y
    @defense = defense
    @name = name
  end
  def draw(win)
    win.setpos(@y, @x)
    win.addch('A')
  end
end

class Game
  # Dungeon generation parameters (adjust these)
  ROOM_MIN_SIZE = 5        # 最小部屋サイズ
  ROOM_MAX_SIZE = 12       # 最大部屋サイズ
  SIZE_MODE = :uniform     # :uniform | :small | :large
  DUNGEON_WIDTH_MIN = 50
  DUNGEON_WIDTH_MAX = 100
  DUNGEON_HEIGHT_MIN = 20
  DUNGEON_HEIGHT_MAX = 25

  # Projectile / effect tuning
  PROJECTILE_STEP_DELAY = 0.02  # 各ステップの表示時間（秒）。小さいほど速く飛ぶ
  HIT_EFFECT_DURATION = 0.2     # 命中時のエフェクト持続時間（秒）

  # 武器・防具の種類と確率
  WEAPON_TABLE = [
    { name: "木の枝", atk: 1,  prob: 0.10 },
    { name: "銅の剣", atk: 3,  prob: 0.20 },
    { name: "鉄の剣", atk: 5,  prob: 0.20 },
    { name: "伝説の剣", atk: 10, prob: 0.05 },
    { name: "女神の剣", atk: 20, prob: 0.01 }
  ]
  ARMOR_TABLE = [
    { name: "鍋蓋", defense: 1,  prob: 0.10 },
    { name: "銅の盾", defense: 3,  prob: 0.20 },
    { name: "鉄の盾", defense: 5,  prob: 0.20 },
    { name: "伝説の盾", defense: 10, prob: 0.05 },
    { name: "女神の盾", defense: 20, prob: 0.01 }
  ]

  def setup
    @win = Curses.stdscr

    Curses.noecho
    Curses.curs_set(0)
    Curses.cbreak

    @win.keypad(true)
    @win.timeout = 50   # 非ブロッキング

    # カラーサポートを初期化（利用可能なら）
    if Curses.respond_to?(:start_color)
      begin
        Curses.start_color
        Curses.init_pair(1, Curses::COLOR_WHITE, Curses::COLOR_BLACK)  # Slime
        Curses.init_pair(2, Curses::COLOR_GREEN, Curses::COLOR_BLACK)  # Goblin
        Curses.init_pair(3, Curses::COLOR_BLUE, Curses::COLOR_BLACK)   # Bat
        Curses.init_pair(4, Curses::COLOR_RED, Curses::COLOR_BLACK)    # Dragon
        Curses.init_pair(5, Curses::COLOR_YELLOW, Curses::COLOR_BLACK) # Item
        Curses.init_pair(6, Curses::COLOR_CYAN, Curses::COLOR_BLACK)   # Stairs
        Curses.init_pair(7, Curses::COLOR_RED, Curses::COLOR_BLACK)    # Player blink (red)
      rescue
        # カラー初期化に失敗しても続行
      end
    end
    # ダンジョンサイズと部屋数をランダムに決定
    @dungeon_width = rand(DUNGEON_WIDTH_MIN..DUNGEON_WIDTH_MAX)
    @dungeon_height = rand(DUNGEON_HEIGHT_MIN..DUNGEON_HEIGHT_MAX)
    @room_count = rand(2..8)

    # ダンジョンを生成（パラメータは上の定数で調整可能）
    @generator = DungeonGenerator.new(@dungeon_width, @dungeon_height,
                      rooms_count: @room_count,
                      min_room_size: ROOM_MIN_SIZE,
                      max_room_size: ROOM_MAX_SIZE,
                      size_mode: SIZE_MODE)
    @map = @generator.generate

    # マップの床位置からプレイヤーのスタート地点を探す
    start_x = 1
    start_y = 1
    @map.each_with_index do |row, y|
      row.each_char.with_index do |char, x|
        if char == '.'
          start_x = x
          start_y = y
          break
        end
      end
      break if start_x > 1  # 最初の床位置が見つかったら終了
    end

    @player = Player.new(start_x, start_y, max_hp = 100)
    @enemies = []
    @message_log = MessageLog.new
    
    # 階層管理
    @floor = 1 if !defined?(@floor) || @floor.nil?
    
    # 敵配置用に後で使用する最大値を保存
    @max_enemies = 5
    
    # レベル上のオブジェクトを初期配置（階段とアイテム）
    setup_level(start_x, start_y)

    # 射撃入力待ちフラグ
    @awaiting_shot = false

    # 敵をランダムに配置（アイテム/階段/プレイヤーと重ならないように）
    @max_enemies.times do
      loop do
        ex = rand(1...@dungeon_width)
        ey = rand(1...@dungeon_height)
        # 床の位置で、プレイヤー・アイテム・階段でない場所に配置
        occupied = (ex == start_x && ey == start_y)
        occupied ||= @items.any? { |it| it.x == ex && it.y == ey }
        occupied ||= (@stairs && @stairs.x == ex && @stairs.y == ey)
        if @map[ey][ex] == '.' && !occupied
          @enemies << spawn_enemy_at(ex, ey)
          break
        end
      end
    end
    
    @running = true
    @game_over = false

    # 初期化時に視界データを確保
    @explored = Array.new(@dungeon_height) { Array.new(@dungeon_width, false) }
    @visible_tiles = Set.new

    # 初期視界更新
    update_fov
  end

  def handle_input
    # 入力バッファをクリアし、最後の入力だけ採用
    @win.nodelay = true
    last_ch = nil
    loop do
      ch = @win.getch
      break if ch.nil?
      last_ch = ch
    end
    @win.nodelay = false
    ch = last_ch || @win.getch
    return false if ch.nil?

    # ゲームオーバー時は操作を受け付けない（qキーのみ受け付け）
    if @game_over
      if ch == 'q' || ch == 'Q'
        @running = false
      end
      return false
    end

    max_x = @dungeon_width
    max_y = @dungeon_height

    # 射撃方向選択中なら矢印キーで射撃を実行
    if @awaiting_shot
      case ch
      when Curses::KEY_UP,    259
        @awaiting_shot = false
        return attempt_shoot(0, -1)
      when Curses::KEY_DOWN,  258
        @awaiting_shot = false
        return attempt_shoot(0,  1)
      when Curses::KEY_LEFT,  260
        @awaiting_shot = false
        return attempt_shoot(-1, 0)
      when Curses::KEY_RIGHT, 261
        @awaiting_shot = false
        return attempt_shoot(1,  0)
      else
        @awaiting_shot = false
        @message_log.add("射撃をキャンセルしました")
        return false
      end
    end

    case ch
    when 'f', 'F'
      if @player.arrows > 0
        @awaiting_shot = true
        @message_log.add("矢の方向を選んでください（矢: #{@player.arrows}）")
        return false
      else
        @message_log.add("矢がない！")
        return false
      end
    when Curses::KEY_UP,    259 then return handle_player_move(0, -1, max_x, max_y)
    when Curses::KEY_DOWN,  258 then return handle_player_move(0,  1, max_x, max_y)
    when Curses::KEY_LEFT,  260 then return handle_player_move(-1, 0, max_x, max_y)
    when Curses::KEY_RIGHT, 261 then return handle_player_move(1,  0, max_x, max_y)
    when 13, 10 then return try_use_stairs  # Enter キー
    when '>', 62 then return try_use_stairs  # > キー (Shift+.)
    when 'q', 'Q'
      @running = false
      return false
    else
      return false
    end
  end

  def handle_player_move(dx, dy, max_x, max_y)
    nx = @player.x + dx
    ny = @player.y + dy

    # 境界チェック
    return false if nx < 0 || nx >= max_x || ny < 0 || ny >= max_y
    
    # 壁チェック
    return false if @map[ny][nx] == '#'

    # 敵との衝突判定
    target_enemy = @enemies.find { |e| e.x == nx && e.y == ny }
    
    if target_enemy
      # 敵にダメージを与える（@atk に依存）
      damage = @player.atk + rand(0..2)
      target_enemy.take_damage(damage)
      # 分裂処理（Splitter専用）
      if target_enemy.name == 'Splitter' && damage > 0 && target_enemy.alive?
        if rand < 0.4
          dirs = [[1,0],[-1,0],[0,1],[0,-1]].shuffle
          split_x, split_y = nil, nil
          dirs.each do |dx, dy|
            sx, sy = target_enemy.x + dx, target_enemy.y + dy
            if sx >= 0 && sy >= 0 && sx < @map[0].size && sy < @map.size && @map[sy][sx] != '#' && @enemies.none? { |e| e.x == sx && e.y == sy } && !(@player.x == sx && @player.y == sy)
              split_x, split_y = sx, sy
              break
            end
          end
          if split_x && split_y
            new_enemy = Enemy.new(split_x, split_y, 'Splitter')
            @enemies << new_enemy
            @message_log.add('Splitterが分裂した！')
          end
        end
      end
      # ダメージを受けた敵を短時間点滅させる（0.25秒）
      target_enemy.flash(0.25) if target_enemy.alive?
      @message_log.add("#{target_enemy.name}に#{damage}のダメージを与えた！")
      if !target_enemy.alive?
        @message_log.add("#{target_enemy.name}を倒した！")
        drop_loot(target_enemy)
        @enemies.delete(target_enemy)
        # 敵撃破時の経験値付与（タイプ別）
        exp_gain = target_enemy.exp_reward
        # 階層ごとに経験値を1.1倍補正
        exp_gain = (exp_gain * (1.1 ** (@floor - 1))).to_i
        @player.gain_exp(exp_gain, @message_log)
      end
      return true  # ターン経過
    else
      # 通常移動
      @player.x = nx
      @player.y = ny
      @player.take_damage(1)
      
      # 移動完了後、アイテムを回収
      check_item_pickup
      
      return true  # ターン経過
    end
  end

  # プレイヤーの射撃処理（方向 dx,dy）
  def attempt_shoot(dx, dy)
    if @player.arrows <= 0
      @message_log.add("矢がない！")
      return false
    end

    # 矢を消費
    @player.add_arrows(-1)
    @message_log.add("矢を1本放った (残り: #{@player.arrows})")

    # 軸に沿ったシンボル
    symbol = dx != 0 ? '-' : '|'

    # 軸方向に進み、壁または範囲外まで座標列を作る
    cells = []
    cx = @player.x + dx
    cy = @player.y + dy
    while cx.between?(0, @dungeon_width - 1) && cy.between?(0, @dungeon_height - 1)
      cells << [cx, cy]
      break if @map[cy][cx] == '#'
      # stop if an enemy is present (we include its cell; simulate will handle hit)
      break if @enemies.any? { |e| e.x == cx && e.y == cy }
      cx += dx
      cy += dy
    end

    damage = @player.atk + rand(0..2)
    simulate_projectile(cells, symbol, damage)
    true
  end
  # アイテム回収処理
  def check_item_pickup
    # 足元のアイテムをチェック
    item = @items.find { |it| it.x == @player.x && it.y == @player.y }
    return unless item

    if item.is_a?(ArrowItem)
      @player.add_arrows(item.count)
      @message_log.add("矢を#{item.count}本拾った！ (残り: #{@player.arrows})")
      @items.delete(item)
      return
    end

    if item.is_a?(WeaponItem)
      @player.equip_weapon(item.atk, item.name)
      @message_log.add("#{item.name}を拾った！ 攻撃力 +#{item.atk}")
      @items.delete(item)
      return
    end

    if item.is_a?(ArmorItem)
      @player.equip_armor(item.defense, item.name)
      @message_log.add("#{item.name}を拾った！ 防御力 +#{item.defense}")
      @items.delete(item)
      return
    end

    # HP回復
    old_hp = @player.current_hp
    @player.current_hp = [@player.current_hp + item.heal_amount, @player.max_hp].min
    restored = @player.current_hp - old_hp

    @message_log.add("薬を拾った！ HP を #{restored} 回復した")
    @items.delete(item)
  end

  # 敵撃破時のドロップ処理
  def drop_loot(enemy)
    return unless enemy
    if enemy.name == 'Goblin'
      @items << ArrowItem.new(enemy.x, enemy.y, 3)
      @message_log.add("#{enemy.name}が矢を3本落とした！")
    end
  end

  # 階段使用処理
  def try_use_stairs
    # 足元の階段をチェック
    return false unless @stairs
    return false unless @stairs.x == @player.x && @stairs.y == @player.y

    # 次の階へ
    next_floor
    return true  # ターン経過
  end

  # 次の階へ移動
  def next_floor
    @floor += 1
    # 30階でゲームクリア
    if @floor > 30
      @game_over = true
      @game_clear = true
      @message_log.add("おめでとう！30階を踏破しゲームクリアです！ 'q'で終了")
      return
    end

    # 階層ごとにダンジョンパラメータを調整
    if @floor <= 5
      @dungeon_width = 40
      @dungeon_height = 20
      @room_count = rand(2..3)
      min_room = 5
      max_room = 5
    elsif @floor <= 20
      @dungeon_width = rand(50..70)
      @dungeon_height = 23
      @room_count = rand(3..6)
      min_room = 5
      max_room = 10
    else
      @dungeon_width = rand(60..100)
      @dungeon_height = 25
      @room_count = rand(3..8)
      min_room = 5
      max_room = 12
    end

    @message_log.add("地下#{@floor}階に降りた")

    # ダンジョンを再生成
    @generator = DungeonGenerator.new(@dungeon_width, @dungeon_height,
                                      rooms_count: @room_count,
                                      min_room_size: min_room,
                                      max_room_size: max_room,
                                      size_mode: SIZE_MODE)
    @map = @generator.generate

    # プレイヤーをスタート地点に移動
    start_x = 1
    start_y = 1
    @map.each_with_index do |row, y|
      row.chars.each_with_index do |char, x|
        if char == '.'
          start_x = x
          start_y = y
          break
        end
      end
      break if start_x > 1
    end

    @player.x = start_x
    @player.y = start_y

    # 視界リセット
    @explored = Array.new(@dungeon_height) { Array.new(@dungeon_width, false) }
    @visible_tiles.clear

    # 敵をリセット
    @enemies.clear
    @max_enemies.times do
      loop do
        ex = rand(1...@dungeon_width)
        ey = rand(1...@dungeon_height)
        occupied = (ex == start_x && ey == start_y)
        if @map[ey][ex] == '.' && !occupied
          @enemies << spawn_enemy_at(ex, ey)
          break
        end
      end
    end

    # レベルを再生成（アイテムと階段）
    setup_level(start_x, start_y)

    # 初期視界を更新
    update_fov
  end

  # レベル上の階段・アイテムを配置する
  def setup_level(start_x, start_y)
    @items = []
    @stairs = nil

    # ヘルパー：位置が部屋内かどうかを判定
    in_room = lambda { |x, y|
      @generator.rooms.any? { |room|
        x >= room.x && x < room.x + room.width &&
        y >= room.y && y < room.y + room.height
      }
    }

    # 階段を1つ配置（部屋内のみ）
    loop do
      sx = rand(1...@dungeon_width)
      sy = rand(1...@dungeon_height)
      next if @map[sy][sx] != '.'
      next if sx == start_x && sy == start_y
      next unless in_room.call(sx, sy)  # 部屋内のみ
      @stairs = Stairs.new(sx, sy)
      break
    end

    # アイテムを1〜7個配置（回復アイテムのみ）
    item_count = rand(1..7)
    while @items.size < item_count
      ix = rand(1...@dungeon_width)
      iy = rand(1...@dungeon_height)
      next if @map[iy][ix] != '.'
      next if ix == start_x && iy == start_y
      next if @stairs && @stairs.x == ix && @stairs.y == iy
      next if @items.any? { |it| it.x == ix && it.y == iy }
      @items << Item.new(ix, iy)
    end

    # 武器（20%の確率で配置）
    if rand < 0.2
      loop do
        wx = rand(1...@dungeon_width)
        wy = rand(1...@dungeon_height)
        next if @map[wy][wx] != '.'
        next if wx == start_x && wy == start_y
        next if @stairs && @stairs.x == wx && @stairs.y == wy
        next if @items.any? { |it| it.x == wx && it.y == wy }
        # 武器種類を確率で決定
        r = rand
        acc = 0.0
        weapon = WEAPON_TABLE.find do |w|
          acc += w[:prob]
          r < acc
        end || WEAPON_TABLE.first
        @items << WeaponItem.new(wx, wy, weapon[:atk], weapon[:name])
        break
      end
    end
    # 防具（20%の確率で配置）
    if rand < 0.2
      loop do
        ax = rand(1...@dungeon_width)
        ay = rand(1...@dungeon_height)
        next if @map[ay][ax] != '.'
        next if ax == start_x && ay == start_y
        next if @stairs && @stairs.x == ax && @stairs.y == ay
        next if @items.any? { |it| it.x == ax && it.y == ay }
        # 防具種類を確率で決定
        r = rand
        acc = 0.0
        armor = ARMOR_TABLE.find do |a|
          acc += a[:prob]
          r < acc
        end || ARMOR_TABLE.first
        @items << ArmorItem.new(ax, ay, armor[:defense], armor[:name])
        break
      end
    end
  end

  # 敵タイプを重み付きで選択（フロア依存）
  def choose_enemy_type
    # フロアごとの敵出現率カスタマイズ
    weights = case @floor
    when 1..6
      # 初期フロア：Slimeが多い
      { 'Slime' => 99, 'Bat' => 1, 'Goblin' => 0, 'Dragon' => 0, 'Splitter' => 0, 'Ghost' => 0 }
    when 7..14
      # 中盤：バランス型
      { 'Slime' => 25, 'Bat' => 30, 'Goblin' => 30, 'Dragon' => 1, 'Splitter' => 10, 'Ghost' => 8 }
    when 15..24
      # 後半：強敵が増える
      { 'Slime' => 8, 'Bat' => 25, 'Goblin' => 35, 'Dragon' => 20, 'Splitter' => 10, 'Ghost' => 10 }
    else
      # 深層：ほぼドラゴン
      { 'Slime' => 3, 'Bat' => 10, 'Goblin' => 25, 'Dragon' => 30, 'Splitter' => 12, 'Ghost' => 25 }
    end

    total = weights.values.sum
    r = rand(total)
    cum = 0
    weights.each do |name, weight|
      cum += weight
      return name if r < cum
    end
    'Slime'
  end

  # 指定位置に敵をスポーン
  def spawn_enemy_at(x, y)
    type = choose_enemy_type
    enemy = Enemy.new(x, y, type)

    # 階層ごとの強さ補正：1階ごとに1.1倍（例：floor1 = x1, floor2 = x1.1, floor3 = x1.21）
    steps = (@floor - 1)
    if steps > 0
      multiplier = (1.1 ** steps)
      # 四捨五入でスケール適用（最低値1を保証）
      enemy.max_hp = [(enemy.max_hp * multiplier).round, 1].max
      enemy.current_hp = enemy.max_hp
      enemy.damage = [(enemy.damage * multiplier).round, 1].max
    end

    enemy
  end

  # 視界（FoV）更新
  def update_fov
    @visible_tiles.clear

    # プレイヤーがどの部屋にいるか判定
    in_room = nil
    if @generator && @generator.rooms
      @generator.rooms.each do |room|
        if @player.x >= room.x && @player.x < room.x + room.width && @player.y >= room.y && @player.y < room.y + room.height
          in_room = room
          break
        end
      end
    end

    if in_room
      # 部屋全体と周囲1マスを可視化
      x0 = [in_room.x - 1, 0].max
      x1 = [in_room.x + in_room.width, @dungeon_width - 1].min
      y0 = [in_room.y - 1, 0].max
      y1 = [in_room.y + in_room.height, @dungeon_height - 1].min

      (y0..y1).each do |yy|
        (x0..x1).each do |xx|
          @visible_tiles.add([xx, yy])
          @explored[yy][xx] = true
        end
      end
    else
      # 通路にいる場合は周囲1マス（3x3）を可視化
      ( -1..1 ).each do |dy|
        ( -1..1 ).each do |dx|
          xx = @player.x + dx
          yy = @player.y + dy
          next unless xx.between?(0, @dungeon_width - 1) && yy.between?(0, @dungeon_height - 1)
          @visible_tiles.add([xx, yy])
          @explored[yy][xx] = true
        end
      end
    end
  end


  def render
    @win.clear
    
    # マップを描画（探索済みのタイルのみ表示）
    @map.each_with_index do |row, y|
      display_row = ''
      (0...@dungeon_width).each do |x|
        if @explored[y][x]
          display_row << @map[y][x]
        else
          display_row << ' '
        end
      end
      @win.setpos(y, 0)
      @win.addstr(display_row)
    end
    
    # 階段とアイテムを描画（現在見えている場合のみ）
    if @stairs && @visible_tiles.include?([@stairs.x, @stairs.y])
      @stairs.draw(@win)
    end

    @items.each do |it|
      if @visible_tiles.include?([it.x, it.y])
        it.draw(@win)
      end
    end


    # 敵を描画（現在見えている場合のみ）
    @enemies.each do |enemy|
      visible = @visible_tiles.include?([enemy.x, enemy.y])
      # Ghostは6マス以内なら必ず見える
      if enemy.name == 'Ghost'
        dist = (enemy.x - @player.x).abs + (enemy.y - @player.y).abs
        visible ||= dist <= 6
      end
      if visible
        enemy.draw(@win)
      end
    end

    # 矢などの一時的な弾道表示（順に1マスずつ黄色）
    # Goblin 等の遠距離攻撃で渡された座標列を、このメソッドで同一ターン内に
    # 順に描画して命中・エフェクト処理を行う（ターンは進まない）
    # color_pair_id を渡すと表示色を変更できる（デフォルトは黄色）
    def simulate_projectile(cells, symbol, damage, color_pair_id = 5)
      cells.each do |cx, cy|
        # フルレンダリングで背景を整えた後に弾を描画
        render
        @win.setpos(cy, cx)
        if Curses.respond_to?(:color_pair)
          Curses.attron(Curses.color_pair(color_pair_id))
          @win.addch(symbol)
          Curses.attroff(Curses.color_pair(color_pair_id))
        else
          @win.addch(symbol)
        end
        @win.refresh

        # 少しだけ時間を置いて弾の動きを視認できるようにする
        sleep(PROJECTILE_STEP_DELAY)

        # プレイヤーに命中
        if @player.x == cx && @player.y == cy
          @player.hurt(damage, :enemy)
          @message_log.add("矢が命中して#{damage}のダメージを受けた！")

          # 命中エフェクト
          @win.setpos(cy, cx)
          if Curses.respond_to?(:color_pair)
            Curses.attron(Curses.color_pair(7))
            @win.addch('*')
            Curses.attroff(Curses.color_pair(7))
          else
            @win.addch('*')
          end
          @win.refresh
          sleep(HIT_EFFECT_DURATION)
          break
        end

        # 他の敵に命中
        hit_enemy = @enemies.find { |e| e.x == cx && e.y == cy }
        if hit_enemy
          damage = @player.atk + rand(0..2)
          hit_enemy.take_damage(damage)
          # 分裂処理（Splitter専用）
          if hit_enemy.name == 'Splitter' && damage > 0 && hit_enemy.alive?
            if rand < 0.4
              dirs = [[1,0],[-1,0],[0,1],[0,-1]].shuffle
              split_x, split_y = nil, nil
              dirs.each do |dx, dy|
                sx, sy = hit_enemy.x + dx, hit_enemy.y + dy
                if sx >= 0 && sy >= 0 && sx < @map[0].size && sy < @map.size && @map[sy][sx] != '#' && @enemies.none? { |e| e.x == sx && e.y == sy } && !(@player.x == sx && @player.y == sy)
                  split_x, split_y = sx, sy
                  break
                end
              end
              if split_x && split_y
                new_enemy = Enemy.new(split_x, split_y, 'Splitter')
                @enemies << new_enemy
                @message_log.add('Splitterが分裂した！')
              end
            end
          end
          hit_enemy.flash(0.25) if hit_enemy.alive?
          @message_log.add("#{hit_enemy.name}に#{damage}のダメージを与えた！")
          if !hit_enemy.alive?
            @message_log.add("#{hit_enemy.name}を倒した！")
            drop_loot(hit_enemy)
            @enemies.delete(hit_enemy)
            exp_gain = hit_enemy.exp_reward
            # 階層ごとに経験値を1.1倍補正
            exp_gain = (exp_gain * (1.1 ** (@floor - 1))).to_i
            @player.gain_exp(exp_gain, @message_log)
          end
          # 命中エフェクト
          @win.setpos(cy, cx)
          if Curses.respond_to?(:color_pair)
            Curses.attron(Curses.color_pair(7))
            @win.addch('*')
            Curses.attroff(Curses.color_pair(7))
          else
            @win.addch('*')
          end
          @win.refresh
          sleep(HIT_EFFECT_DURATION)
          break
        end

        # 壁に衝突した場合は終了（念のため）
        if @map[cy][cx] == '#'
          @win.setpos(cy, cx)
          if Curses.respond_to?(:color_pair)
            Curses.attron(Curses.color_pair(7))
            @win.addch('*')
            Curses.attroff(Curses.color_pair(7))
          else
            @win.addch('*')
          end
          @win.refresh
          sleep(HIT_EFFECT_DURATION)
          break
        end
      end

      # 最後に全面を再描画して画面を整える
      render
    end



    # プレイヤーを描画
    @player.draw(@win)
    
    # ステータス情報（HP / レベル / 攻撃力 / 経験値 / 階層）
    @win.setpos(@dungeon_height, 0)
    status = "[HP: #{@player.current_hp}/#{@player.max_hp}] " +
         "Lv: #{@player.level} " +
         "Atk: #{@player.atk}"
    if @player.weapon_atk > 0
      status += "(+#{@player.weapon_atk}: #{@player.weapon_name}) "
    else
      status += " "
    end
    status += "Def: #{@player.total_defense} (#{@player.armor_name}) " +
         "Exp: #{@player.exp}/#{@player.exp_to_next} " +
         "Floor: #{@floor} " +
         "Arrows: #{@player.arrows}"
    @win.addstr(status)

    # メッセージログを描画
    @message_log.draw(@win, @dungeon_height + 1)
    
    # ゲームオーバー画面
    if @game_over
      center_x = @dungeon_width / 2 - 5
      center_y = @dungeon_height / 2
      if defined?(@game_clear) && @game_clear
        @win.setpos(center_y, center_x - 2)
        @win.addstr("GAME CLEAR!")
        @win.setpos(center_y + 1, center_x - 6)
        @win.addstr("Press 'q' to quit")
      else
        @win.setpos(center_y, center_x)
        @win.addstr("GAME OVER")
        @win.setpos(center_y + 1, center_x - 3)
        @win.addstr("Press 'q' to quit")
      end
    end
    
    # ヘルプ情報
    @win.setpos(@dungeon_height + 4, 0)
    @win.addstr("Use arrow keys, f then arrow to shoot, q to quit | Enemies: #{@enemies.length}")
    @win.refresh
  end

  def update
    # 敵のAI処理（プレイヤーに近づく）
    max_x = @dungeon_width
    max_y = @dungeon_height
    
    @enemies.each do |enemy|
      # 各敵のターン行動（特殊能力はここで処理される）
      result = enemy.act(@player, max_x, max_y, @map, @enemies, @message_log)
      if result.is_a?(Hash) && result[:cells]
        # 発射されたらその場で弾道を即時シミュレート（同一ターン内で完了させる）
        simulate_projectile(result[:cells], result[:symbol], result[:damage], result[:color] || 5)
      end
    end



    # 敵によるプレイヤー攻撃判定（隣接している敵がダメージを与える）
    @enemies.each do |enemy|
      distance = (enemy.x - @player.x).abs + (enemy.y - @player.y).abs
      if distance == 1
        damage = enemy.damage
        @player.hurt(damage, :enemy)
        @message_log.add("#{enemy.name}に#{damage}のダメージを受けた！")
      end
    end

    # 敵が死亡したかチェックして処理（ドロップなど）
    @enemies.dup.each do |enemy|
      next if enemy.alive?
      drop_loot(enemy)
      @enemies.delete(enemy)
    end

    # プレイヤーが死亡したかチェック
    if !@player.alive?
      @game_over = true
    end

    # プレイヤーのターン終了処理（点滅カウンタの減少など）
    @player.tick
  end

  def run
    setup
    begin
      while @running
        # プレイヤーがターンを消費する行動をしたかチェック
        turn_passed = handle_input
        
        # ターンが経過した場合のみ敵のAIと判定を実行
        if turn_passed
            update
            # プレイヤー行動後に視界を更新
            update_fov
        end
        
        render
      end
    ensure
      Curses.echo
      Curses.curs_set(1)
      Curses.close_screen
    end
  end
end

Game.new.run