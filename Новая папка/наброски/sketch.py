# -*- coding: utf-8 -*-
"""Наброски схем оверлея под Mansion, Facility и Gordion.

Палитра ровно та, которую разбирает мод (см. MapImages.Tinted):
  белый  a=30  — фон помещений, красится в цвет темы и темнеет при выключенном щитке
  белый  a=255 — обводка и стены, красится в цвет темы
  серый  130   — механика: трубы, колонны, рельсы; тема вполсилы
  жёлтый       — ТОЛЬКО лампы, мод вынимает их в отдельный слой
Рисуем без сглаживания: любой промежуточный оттенок мод счёл бы за другую роль.
"""
import os
from PIL import Image, ImageDraw

W, H = 330, 400
BG   = (255, 255, 255, 30)
LINE = (255, 255, 255, 255)
MECH = (130, 130, 130, 255)
LAMP = (255, 217, 38, 255)
NONE = (0, 0, 0, 0)

OUT = os.path.join('Новая папка', 'наброски')


class Sheet:
    def __init__(self):
        self.im = Image.new('RGBA', (W, H), NONE)
        self.d = ImageDraw.Draw(self.im)

    def fill(self, x1, y1, x2, y2):
        self.d.rectangle([x1, y1, x2, y2], fill=BG)

    def poly_fill(self, pts):
        self.d.polygon(pts, fill=BG)

    def ln(self, x1, y1, x2, y2, th=2, c=LINE):
        self.d.line([x1, y1, x2, y2], fill=c, width=th)

    def box(self, x1, y1, x2, y2, th=2, c=LINE, bg=False):
        if bg:
            self.d.rectangle([x1, y1, x2, y2], fill=BG)
        self.d.rectangle([x1, y1, x2, y2], outline=c, width=th)

    def dot(self, x, y, r, c=LINE):
        self.d.ellipse([x - r, y - r, x + r, y + r], outline=c, width=2)

    def blob(self, x, y, r, c=LINE):
        self.d.ellipse([x - r, y - r, x + r, y + r], fill=c)

    def save(self, name):
        os.makedirs(OUT, exist_ok=True)
        p = os.path.join(OUT, name)
        self.im.save(p)
        return p


def tufts(s, xs, y, h=7):
    """Кустики травы — те же, что на схеме шахты."""
    for x in xs:
        s.ln(x, y, x - 4, y - h)
        s.ln(x, y, x, y - h - 2)
        s.ln(x, y, x + 4, y - h)


# ======================================================================== MANSON
def mansion():
    s = Sheet()
    PEAK, EAVE_Y = 165, 88
    LX, RX = 48, 282           # свесы крыши
    FLOOR, BALC = 250, 150     # пол первого этажа и уровень балконов

    # фон дома: треугольник крыши плюс тело между колоннами
    s.poly_fill([(PEAK, 14), (RX, EAVE_Y), (LX, EAVE_Y)])
    s.fill(LX + 16, EAVE_Y, RX - 16, FLOOR)

    # крыша
    s.ln(PEAK, 14, LX, EAVE_Y, 3)
    s.ln(PEAK, 14, RX, EAVE_Y, 3)
    s.ln(LX, EAVE_Y, LX + 20, EAVE_Y, 2)
    s.ln(RX - 20, EAVE_Y, RX, EAVE_Y, 2)

    # колонны с капителями и базами
    for cx in (LX + 8, RX - 8):
        s.box(cx - 12, EAVE_Y - 2, cx + 12, EAVE_Y + 16, 2, bg=True)   # капитель
        s.ln(cx - 6, EAVE_Y + 16, cx - 6, FLOOR - 14, 2)
        s.ln(cx + 6, EAVE_Y + 16, cx + 6, FLOOR - 14, 2)
        s.box(cx - 13, FLOOR - 14, cx + 13, FLOOR, 2, bg=True)         # база

    # пол первого этажа и перекрытие второго
    s.ln(LX + 16, FLOOR, RX - 16, FLOOR, 2)

    # балконы «как в театре»: настил и балюстрада
    for x1, x2 in ((66, 110), (220, 264)):
        s.fill(x1, BALC, x2, BALC + 8)
        s.ln(x1, BALC, x2, BALC, 2)
        s.ln(x1, BALC + 8, x2, BALC + 8, 2)
        for bx in range(x1 + 5, x2 - 2, 9):
            s.ln(bx, BALC - 12, bx, BALC, 2)                            # балясины
        s.ln(x1, BALC - 12, x2, BALC - 12, 2)                           # поручень

    # Парадная лестница: марш из центра пола до площадки, оттуда развилка к
    # балконам. Объём заливаем ПЕРЕД ступенями — иначе фон их затирает.
    s.fill(142, 170, 188, FLOOR)
    s.ln(142, FLOOR, 142, 170, 2)
    s.ln(188, FLOOR, 188, 170, 2)
    for i in range(8):                                                  # ступени марша
        y = FLOOR - 8 - i * 10
        s.ln(142, y, 188, y, 2)

    s.box(128, 158, 202, 172, 2, bg=True)                               # площадка

    for sign in (-1, 1):                                                # два всхода к балконам
        x0 = 128 if sign < 0 else 202
        x1 = 108 if sign < 0 else 222
        s.poly_fill([(x0, 158), (x1, BALC - 2), (x1, BALC + 8), (x0, 172)])
        s.ln(x0, 158, x1, BALC - 2, 2)
        s.ln(x0, 172, x1, BALC + 8, 2)
        for i in range(1, 5):                                           # ступени всхода
            t = i / 5.0
            xa = x0 + (x1 - x0) * t
            ya = 158 + (BALC - 2 - 158) * t
            s.ln(xa, ya, xa, ya + 14, 2)

    # люстра: подвес, ярус рожков, огоньки
    s.ln(PEAK, 20, PEAK, 52, 2, LAMP)
    s.ln(PEAK - 26, 66, PEAK + 26, 66, 2, LAMP)
    s.ln(PEAK - 20, 52, PEAK + 20, 52, 2, LAMP)
    for dx in (-26, -13, 0, 13, 26):
        s.ln(PEAK + dx, 66, PEAK + dx, 78, 2, LAMP)
        s.blob(PEAK + dx, 82, 3, LAMP)
    s.ln(PEAK - 20, 52, PEAK - 26, 66, 2, LAMP)
    s.ln(PEAK + 20, 52, PEAK + 26, 66, 2, LAMP)

    # ---- улица: линия земли, крыльцо, заснеженная тропа, луг ----
    s.ln(0, FLOOR + 2, W, FLOOR + 2, 2)
    tufts(s, [14, 26, 300, 314], FLOOR + 2)

    s.box(85, 272, 245, 312, 2, bg=True)                                # крыльцо
    s.ln(160, 272, 160, 312, 2)
    s.ln(170, 272, 170, 312, 2)

    # тропа развилкой вниз-влево и вниз-вправо
    for sign in (-1, 1):
        x0, y0 = 165 + sign * 6, 316
        for i in range(5):
            t = i / 4.0
            x = x0 + sign * (30 + 100 * t)
            y = 322 + 62 * t
            s.ln(x - sign * 16, y - 10, x, y, 2)
            s.ln(x, y, x + sign * 16, y - 10, 2)

    s.fill(20, 330, 92, 356)                                            # проплешины луга
    s.fill(238, 330, 310, 356)
    s.fill(120, 366, 210, 392)
    tufts(s, [30, 44, 58, 272, 286, 300], 356)
    tufts(s, [134, 150, 166, 182, 198], 392)
    return s


def mansion_guide():
    s = Sheet()
    slots = [(95, 232, '1'), (140, 232, '3'), (205, 232, '4'), (240, 232, '2'),
             (165, 196, '5'),
             (90, 138, '8'), (120, 138, '6'), (210, 138, '7'), (243, 138, '9'),
             (165, 104, '10')]
    for x, y, n in slots:
        s.dot(x, y, 9)
        s.d.text((x - 3, y - 5), n, fill=LINE)
    for x in (40, 100, 230, 290):                                        # уличные
        s.dot(x, 340, 9, MECH)
    return s


# ====================================================================== FACILITY
def facility():
    s = Sheet()
    GROUND = 96

    # ---- улица сбоку: та же линия земли, что у шахты ----
    s.ln(0, GROUND, W, GROUND, 2)
    tufts(s, [16, 30, 44, 96, 110, 124, 170], GROUND)

    s.fill(205, 40, 300, GROUND)                                        # наземный вход
    s.ln(205, 40, 300, 40, 2)
    s.ln(205, 40, 205, GROUND, 2)
    s.ln(300, 40, 300, GROUND, 2)
    s.box(238, 62, 268, GROUND, 2)                                      # ворота
    s.ln(253, 62, 253, GROUND, 2)
    s.ln(214, 22, 291, 22, 2)                                           # козырёк
    s.ln(214, 22, 205, 40, 2)
    s.ln(291, 22, 300, 40, 2)

    s.blob(62, GROUND - 2, 3, MECH)                                     # место корабля
    s.ln(50, GROUND - 2, 74, GROUND - 2, 2, MECH)

    # ствол лестницы от входа вниз к плану
    s.ln(238, GROUND, 238, 124, 2, MECH)
    s.ln(268, GROUND, 268, 124, 2, MECH)
    for y in range(104, 124, 5):
        s.ln(238, y, 268, y, 2, MECH)

    # ---- план комплекса, вид сверху ----
    rooms = [(20, 130, 120, 200), (140, 130, 212, 186), (232, 130, 312, 190),
             (20, 240, 92, 300), (210, 238, 312, 296),
             (30, 338, 84, 386), (102, 338, 156, 386),
             (176, 338, 230, 386), (248, 338, 302, 386)]
    for r in rooms:
        s.box(r[0], r[1], r[2], r[3], 2, bg=True)

    APP = (112, 232, 192, 302)                                          # зал аппарата
    s.box(*APP, th=3, bg=True)
    s.box(140, 258, 164, 278, 2)                                        # постамент, сам аппарат рисует код

    for y1, y2 in ((208, 222), (312, 326)):                             # магистральные коридоры
        s.fill(20, y1, 312, y2)
        s.ln(20, y1, 312, y1, 2)
        s.ln(20, y2, 312, y2, 2)

    links = [(66, 200, 66, 208), (176, 186, 176, 208), (272, 190, 272, 208),
             (56, 222, 56, 240), (152, 222, 152, 232), (260, 222, 260, 238),
             (56, 300, 56, 312), (152, 302, 152, 312), (260, 296, 260, 312),
             (57, 326, 57, 338), (129, 326, 129, 338),
             (203, 326, 203, 338), (275, 326, 275, 338)]
    for x1, y1, x2, y2 in links:                                        # проходы между помещениями
        s.fill(x1 - 7, y1, x1 + 7, y2)
        s.ln(x1 - 7, y1, x1 - 7, y2, 2)
        s.ln(x1 + 7, y1, x1 + 7, y2, 2)

    # лабиринт узких проходов в правой части
    maze = [(200, 208, 200, 160), (200, 160, 226, 160), (226, 160, 226, 130),
            (96, 222, 96, 268), (96, 268, 112, 268)]
    for x1, y1, x2, y2 in maze:
        s.ln(x1, y1, x2, y2, 2)

    # трубы вдоль коридоров, на них вентили
    for y in (215, 319):
        s.ln(24, y, 308, y, 2, MECH)
    for x in (24, 308):
        s.ln(x, 215, x, 319, 2, MECH)
    for vx, vy in ((84, 215), (216, 215), (128, 319), (252, 319)):      # вентили
        s.dot(vx, vy, 5, MECH)
        s.ln(vx - 8, vy, vx + 8, vy, 2, MECH)

    # пожарные выходы по краям
    for x1, y1, x2, y2 in ((8, 258, 20, 282), (312, 254, 322, 278)):
        s.box(x1, y1, x2, y2, 2, bg=True)

    # Потолочные лампы: сверху они читаются короткими полосами. Мод найдёт их по
    # жёлтому и погасит вместе со щитком.
    for lx, ly in ((56, 152), (86, 172), (176, 152), (272, 152),
                   (152, 246), (56, 268), (260, 264),
                   (57, 358), (129, 358), (203, 358), (275, 358)):
        s.ln(lx - 9, ly, lx + 9, ly, 3, LAMP)
    return s


def facility_guide():
    s = Sheet()
    slots = [(70, 165), (176, 158), (272, 160), (56, 270), (260, 267),
             (57, 362), (129, 362), (203, 362), (275, 362), (152, 215)]
    for i, (x, y) in enumerate(slots):
        s.dot(x, y, 9)
        s.d.text((x - 3, y - 5), str(i + 1), fill=LINE)
    for x in (30, 90, 150, 190):                                        # уличные
        s.dot(x, 78, 9, MECH)
    for vx, vy in ((84, 215), (216, 215), (128, 319), (252, 319)):      # вентили: сюда дымку
        s.dot(vx, vy, 13, LAMP)
    s.box(140, 258, 164, 278, 2, LAMP)                                  # ниша аппарата
    return s


# ======================================================================= GORDION
def gordion():
    s = Sheet()
    DECK, WALL = 96, 252

    # ---- стена компании: справа, обрывается только краем оверлея ----
    s.fill(WALL, 0, W, 360)
    s.ln(WALL, 0, WALL, 360, 3)
    s.ln(WALL, 360, W, 360, 3)
    for y in range(18, 356, 26):                                        # кладка
        s.ln(WALL + 4, y, W, y, 2, MECH)

    s.box(256, 48, 302, 92, 3, bg=True)                                 # люк сдачи
    s.ln(256, 56, 302, 56, 2)                                           # притолока
    for x in range(262, 300, 8):                                        # створка
        s.ln(x, 60, x, 88, 2, MECH)

    # ---- палуба: та же высота, что земля шахты ----
    s.fill(8, DECK, WALL, DECK + 10)
    s.ln(8, DECK, WALL, DECK, 3)
    s.ln(8, DECK + 10, WALL, DECK + 10, 2)
    s.ln(8, DECK, 8, DECK + 10, 2)

    for x in range(20, WALL - 6, 16):                                   # настил
        s.ln(x, DECK + 1, x, DECK + 9, 2, MECH)

    s.box(6, DECK - 26, 22, DECK, 2)                                    # тумба у края
    s.box(WALL - 22, DECK - 20, WALL - 6, DECK, 2)

    # места кораблей: игроков слева, дропшип посередине
    for cx in (62, 150):
        s.ln(cx - 14, DECK - 2, cx + 14, DECK - 2, 2, MECH)
        s.blob(cx, DECK - 2, 3, MECH)

    # ---- опоры моста с перекрёстными раскосами ----
    COLS, BOT = (40, 100, 160, 215), 340
    for cx in COLS:
        s.ln(cx - 5, DECK + 10, cx - 5, BOT, 2, MECH)
        s.ln(cx + 5, DECK + 10, cx + 5, BOT, 2, MECH)
        s.ln(cx - 9, BOT, cx + 9, BOT, 2, MECH)
    for a, b in zip(COLS, COLS[1:]):
        for y1, y2 in ((DECK + 24, 200), (200, 316)):
            s.ln(a + 5, y1, b - 5, y2, 2, MECH)
            s.ln(b - 5, y1, a + 5, y2, 2, MECH)
            s.ln(a + 5, y2, b - 5, y2, 2, MECH)
    s.ln(COLS[-1] + 5, DECK + 24, WALL, DECK + 24, 2, MECH)             # упор в стену
    s.ln(COLS[-1] + 5, 200, WALL, 200, 2, MECH)

    # Ограждение вдоль палубы: невысокое, чтобы не спорить с иконками монстров,
    # которые будут ходить здесь же.
    s.ln(8, DECK - 14, WALL - 24, DECK - 14, 2)
    for rx in range(14, WALL - 24, 22):
        s.ln(rx, DECK - 14, rx, DECK, 2)

    s.ln(0, 44, 8, 44, 2, MECH)                                         # дальний горизонт
    s.ln(0, 44, 0, DECK, 2, MECH)

    for lx in (272, 300):                                               # фонари на стене
        s.ln(lx, 108, lx, 116, 2, MECH)
        s.ln(lx - 7, 118, lx + 7, 118, 3, LAMP)
    return s


def gordion_guide():
    s = Sheet()
    for i, (x, y) in enumerate([(40, 74), (95, 74), (200, 74), (228, 74)]):
        s.dot(x, y, 9)
        s.d.text((x - 3, y - 5), str(i + 1), fill=LINE)
    for cx in (62, 150):                                                # корабли
        s.box(cx - 26, 44, cx + 26, 96, 2, MECH)
    s.box(256, 48, 302, 92, 3, LAMP)                                    # люк: створка и щупальца
    return s


if __name__ == '__main__':
    for fn, name in ((mansion, 'mansion-overlay.png'), (mansion_guide, 'mansion-guide.png'),
                     (facility, 'facility-overlay.png'), (facility_guide, 'facility-guide.png'),
                     (gordion, 'gordion-overlay.png'), (gordion_guide, 'gordion-guide.png')):
        print('->', fn().save(name))
