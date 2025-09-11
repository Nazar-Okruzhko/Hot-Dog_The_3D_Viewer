import pygame
import math
import random
import os
import numpy as np
from pygame.locals import *

# Initialize Pygame
pygame.init()

# Screen dimensions with minimum size
MIN_WIDTH, MIN_HEIGHT = 800, 600
WIDTH, HEIGHT = 1200, 800
screen = pygame.display.set_mode((WIDTH, HEIGHT), pygame.RESIZABLE)
pygame.display.set_caption("Hot-Dog")

# Colors (using hex values as requested)
BACKGROUND = (220, 221, 223)  # #DCDDDF
PANEL_BG = (230, 230, 230)    # #E6E6E6
BUTTON_BG = (220, 220, 220)
BUTTON_HOVER = (200, 200, 200)
BUTTON_ACTIVE = (180, 180, 180)
BUTTON_DISABLED = (240, 240, 240)
TEXT_COLOR = (40, 40, 40)
TEXT_DISABLED = (180, 180, 180)
ACCENT = (0, 120, 215)
LIGHT_ACCENT = (30, 150, 240)
GRID_COLOR = (200, 200, 200)
AXIS_X = (255, 100, 100)
AXIS_Y = (100, 255, 100)
AXIS_Z = (100, 100, 255)

# Fonts
font_large = pygame.font.SysFont("Arial", 24)
font_medium = pygame.font.SysFont("Arial", 20)
font_small = pygame.font.SysFont("Arial", 16)

# 3D object representation with performance optimizations
class Mesh:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.normals = []
        self.colors = []
        self.texture = None
        self.bounding_box = [(-1, -1, -1), (1, 1, 1)]
        self.vertex_cache = {}
        self.generate_cube()
        
    def calculate_bounding_box(self):
        if not self.vertices:
            self.bounding_box = [(-1, -1, -1), (1, 1, 1)]
            return
            
        min_x = min(v[0] for v in self.vertices)
        min_y = min(v[1] for v in self.vertices)
        min_z = min(v[2] for v in self.vertices)
        max_x = max(v[0] for v in self.vertices)
        max_y = max(v[1] for v in self.vertices)
        max_z = max(v[2] for v in self.vertices)
        
        self.bounding_box = [(min_x, min_y, min_z), (max_x, max_y, max_z)]
        
    def get_size(self):
        min_pos, max_pos = self.bounding_box
        return (
            max_pos[0] - min_pos[0],
            max_pos[1] - min_pos[1],
            max_pos[2] - min_pos[2]
        )
        
    def get_center(self):
        min_pos, max_pos = self.bounding_box
        return (
            (min_pos[0] + max_pos[0]) / 2,
            (min_pos[1] + max_pos[1]) / 2,
            (min_pos[2] + max_pos[2]) / 2
        )
        
    def generate_cube(self):
        # Define vertices for a cube
        self.vertices = [
            (-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
            (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)
        ]
        
        # Define faces (triangles)
        self.faces = [
            (0, 1, 2), (0, 2, 3),  # front
            (4, 5, 6), (4, 6, 7),  # back
            (0, 4, 7), (0, 7, 3),  # left
            (1, 5, 6), (1, 6, 2),  # right
            (3, 2, 6), (3, 6, 7),  # top
            (0, 1, 5), (0, 5, 4)   # bottom
        ]
        
        # Define normals for each face
        self.normals = [
            (0, 0, -1), (0, 0, -1),
            (0, 0, 1), (0, 0, 1),
            (-1, 0, 0), (-1, 0, 0),
            (1, 0, 0), (1, 0, 0),
            (0, 1, 0), (0, 1, 0),
            (0, -1, 0), (0, -1, 0)
        ]
        
        # Define colors for each face
        self.colors = [
            (255, 0, 0), (200, 0, 0),
            (0, 255, 0), (0, 200, 0),
            (0, 0, 255), (0, 0, 200),
            (255, 255, 0), (200, 200, 0),
            (255, 0, 255), (200, 0, 200),
            (0, 255, 255), (0, 200, 200)
        ]
        
        self.calculate_bounding_box()
        self.vertex_cache = {}
        
    def generate_sphere(self, resolution=20):  # Increased resolution for better sphere quality
        self.vertices = []
        self.faces = []
        self.normals = []
        self.colors = []
        self.vertex_cache = {}
        
        # Generate vertices - fixed to avoid missing faces
        for i in range(resolution + 1):
            lat = math.pi * i / resolution
            sin_lat = math.sin(lat)
            cos_lat = math.cos(lat)
            
            for j in range(resolution + 1):
                lon = 2 * math.pi * j / resolution
                sin_lon = math.sin(lon)
                cos_lon = math.cos(lon)
                
                x = sin_lat * cos_lon
                y = cos_lat  # This makes y the "up" axis
                z = sin_lat * sin_lon
                
                self.vertices.append((x, y, z))
                self.normals.append((x, y, z))
                
        # Generate faces with proper winding order - fixed to avoid gaps
        for i in range(resolution):
            for j in range(resolution):
                v1 = i * (resolution + 1) + j
                v2 = v1 + 1
                v3 = v1 + (resolution + 1)
                v4 = v2 + (resolution + 1)
                
                # Create two triangles for each quad with consistent winding
                self.faces.append((v1, v2, v3))
                self.faces.append((v2, v4, v3))
                
        # Generate colors
        for i in range(len(self.faces)):
            self.colors.append((
                random.randint(100, 255),
                random.randint(100, 255),
                random.randint(100, 255)
            ))
            
        self.calculate_bounding_box()
        
    def load_from_obj(self, file_path):
        try:
            self.vertices = []
            self.faces = []
            self.normals = []
            self.colors = []
            self.vertex_cache = {}
            
            with open(file_path, 'r') as f:
                for line in f:
                    if line.startswith('v '):
                        parts = line.strip().split()
                        if len(parts) >= 4:
                            # Flip Y and Z coordinates to fix upside-down issue
                            self.vertices.append((float(parts[1]), -float(parts[3]), float(parts[2])))
                    elif line.startswith('f '):
                        parts = line.strip().split()
                        if len(parts) >= 4:
                            # Handle different face formats (v, v/vt, v/vt/vn)
                            face_vertices = []
                            for i in range(1, len(parts)):
                                vertex_parts = parts[i].split('/')
                                face_vertices.append(int(vertex_parts[0]) - 1)  # OBJ indices start at 1
                            
                            # Triangulate the face if it has more than 3 vertices
                            if len(face_vertices) > 3:
                                for j in range(1, len(face_vertices) - 1):
                                    self.faces.append((face_vertices[0], face_vertices[j], face_vertices[j + 1]))
                            else:
                                self.faces.append(tuple(face_vertices))
            
            # Generate normals
            self.normals = []
            for i, face in enumerate(self.faces):
                if len(face) >= 3:
                    v1 = self.vertices[face[0]]
                    v2 = self.vertices[face[1]]
                    v3 = self.vertices[face[2]]
                    
                    u = (v2[0] - v1[0], v2[1] - v1[1], v2[2] - v1[2])
                    v = (v3[0] - v1[0], v3[1] - v1[1], v3[2] - v1[2])
                    
                    normal = (
                        u[1] * v[2] - u[2] * v[1],
                        u[2] * v[0] - u[0] * v[2],
                        u[0] * v[1] - u[1] * v[0]
                    )
                    
                    # Normalize
                    length = math.sqrt(normal[0]**2 + normal[1]**2 + normal[2]**2)
                    if length > 0:
                        normal = (normal[0]/length, normal[1]/length, normal[2]/length)
                    else:
                        normal = (0, 0, 1)
                    
                    self.normals.append(normal)
                else:
                    self.normals.append((0, 0, 1))
            
            # Generate colors
            self.colors = []
            for i in range(len(self.faces)):
                self.colors.append((
                    random.randint(100, 255),
                    random.randint(100, 255),
                    random.randint(100, 255)
                ))
                
            self.calculate_bounding_box()
            return True
            
        except Exception as e:
            print(f"Error loading OBJ file: {e}")
            return False

# 3D Camera with fixed rotation
class Camera:
    def __init__(self):
        self.x = 0
        self.y = 0
        self.z = -5
        self.rot_x = 0
        self.rot_y = 0
        self.rot_z = 0
        self.fov = 60
        self.near = 0.1
        self.far = 100.0
        self.pan_x = 0
        self.pan_y = 0
        self.last_rot_x = 0
        self.last_rot_y = 0
        self.moved = True
        self.cache_key = None
        
    def project(self, point, cache_key=None):
        # Apply rotation
        x, y, z = point
        
        # Rotate around Y axis first (horizontal)
        x_rot = x * math.cos(self.rot_y) - z * math.sin(self.rot_y)
        z_rot = x * math.sin(self.rot_y) + z * math.cos(self.rot_y)
        x, z = x_rot, z_rot
        
        # Then rotate around X axis (vertical)
        y_rot = y * math.cos(self.rot_x) - z * math.sin(self.rot_x)
        z_rot = y * math.sin(self.rot_x) + z * math.cos(self.rot_x)
        y, z = y_rot, z_rot
        
        # Apply translation
        x += self.x + self.pan_x
        y += self.y + self.pan_y
        z += self.z
        
        # Perspective projection - fixed to correct upside-down issue
        factor = self.fov / (z + 1e-5)
        x_proj = x * factor
        y_proj = y * factor
        
        # Scale to screen - removed Y inversion to fix upside-down issue
        x_screen = WIDTH // 2 + int(x_proj * 100)
        y_screen = HEIGHT // 2 + int(y_proj * 100)  # Removed the minus sign
        
        return (x_screen, y_screen, z)
        
    def auto_position(self, mesh):
        # Automatically position camera based on mesh size
        size = mesh.get_size()
        center = mesh.get_center()
        
        # Calculate distance needed to view the entire mesh
        max_dim = max(size[0], size[1], size[2])
        self.z = -max_dim * 2.5  # Adjust multiplier as needed
        
        # Center camera on mesh
        self.pan_x = -center[0]
        self.pan_y = -center[1]
        self.x = 0
        self.y = 0
        self.rot_x = 0
        self.rot_y = 0
        self.moved = True

# UI Components
class Button:
    def __init__(self, x, y, width, height, text, icon=None, auto_resize=True):
        self.rect = pygame.Rect(x, y, width, height)
        self.text = text
        self.icon = icon
        self.active = False
        self.hover = False
        self.enabled = True
        self.auto_resize = auto_resize
        self.original_rect = pygame.Rect(x, y, width, height)  # Store original position for resizing
        
        # Auto-resize button to fit text if needed
        if auto_resize and text:
            text_width = font_medium.size(text)[0] + 20  # Add padding
            if text_width > width:
                self.rect.width = text_width
                self.original_rect.width = text_width
        
    def update_position(self, width_ratio, height_ratio):
        # Update position based on window resize
        self.rect.x = int(self.original_rect.x * width_ratio)
        self.rect.y = int(self.original_rect.y * height_ratio)
        self.rect.width = int(self.original_rect.width * width_ratio)
        self.rect.height = int(self.original_rect.height * height_ratio)
        
    def draw(self, screen):
        if not self.enabled:
            color = BUTTON_DISABLED
            text_color = TEXT_DISABLED
        else:
            color = BUTTON_ACTIVE if self.active else BUTTON_HOVER if self.hover else BUTTON_BG
            text_color = TEXT_COLOR
            
        pygame.draw.rect(screen, color, self.rect, border_radius=4)
        pygame.draw.rect(screen, (180, 180, 180), self.rect, 1, border_radius=4)
        
        if self.text:
            text_surf = font_medium.render(self.text, True, text_color)
            # Scale text to fit button if needed
            text_width = text_surf.get_width()
            if text_width > self.rect.width - 10 and self.auto_resize:
                # Find the best font size that fits
                for size in range(20, 10, -1):
                    test_font = pygame.font.SysFont("Arial", size)
                    test_surf = test_font.render(self.text, True, text_color)
                    if test_surf.get_width() <= self.rect.width - 10:
                        text_surf = test_surf
                        break
            
            text_rect = text_surf.get_rect(center=self.rect.center)
            screen.blit(text_surf, text_rect)
            
    def check_hover(self, pos):
        self.hover = self.rect.collidepoint(pos) and self.enabled
        return self.hover
        
    def check_click(self, pos, event):
        if event.type == MOUSEBUTTONDOWN and event.button == 1 and self.enabled:
            if self.rect.collidepoint(pos):
                self.active = not self.active
                return True
        return False

class LightControl:
    def __init__(self, x, y, radius):
        self.rect = pygame.Rect(x - radius, y - radius, radius * 2, radius * 2)
        self.radius = radius
        self.center = (x, y)
        self.original_center = (x, y)  # Store original position for resizing
        self.angle = 0.0
        self.dragging = False
        
    def update_position(self, width_ratio, height_ratio):
        # Update position based on window resize
        self.center = (int(self.original_center[0] * width_ratio), 
                       int(self.original_center[1] * height_ratio))
        self.rect = pygame.Rect(self.center[0] - self.radius, 
                               self.center[1] - self.radius, 
                               self.radius * 2, self.radius * 2)
        
    def draw(self, screen):
        # Draw circle
        pygame.draw.circle(screen, BUTTON_BG, self.center, self.radius)
        pygame.draw.circle(screen, (180, 180, 180), self.center, self.radius, 2)
        
        # Draw light direction
        lx = self.center[0] + int(math.cos(self.angle) * (self.radius - 10))
        ly = self.center[1] - int(math.sin(self.angle) * (self.radius - 10))
        pygame.draw.line(screen, LIGHT_ACCENT, self.center, (lx, ly), 2)
        pygame.draw.circle(screen, (255, 220, 100), (lx, ly), 8)
        
    def check_click(self, pos, event):
        # Check if click is within the circle
        dx = pos[0] - self.center[0]
        dy = self.center[1] - pos[1]  # Invert y for proper angle calculation
        distance = math.sqrt(dx*dx + dy*dy)
        
        if event.type == MOUSEBUTTONDOWN and event.button == 1:
            if distance <= self.radius:
                self.dragging = True
                self.update_angle(pos)
                return True
                
        if event.type == MOUSEBUTTONUP and event.button == 1:
            self.dragging = False
            
        if event.type == MOUSEMOTION and self.dragging:
            self.update_angle(pos)
            return True
            
        return False
        
    def update_angle(self, pos):
        dx = pos[0] - self.center[0]
        dy = self.center[1] - pos[1]  # Invert y for proper angle calculation
        self.angle = math.atan2(dy, dx)

class TabView:
    def __init__(self, x, y, width, height, tabs):
        self.rect = pygame.Rect(x, y, width, height)
        self.original_rect = pygame.Rect(x, y, width, height)  # Store original position for resizing
        self.tabs = tabs
        self.active_tab = 0
        
    def update_position(self, width_ratio, height_ratio):
        # Update position based on window resize
        self.rect.x = int(self.original_rect.x * width_ratio)
        self.rect.y = int(self.original_rect.y * height_ratio)
        self.rect.width = int(self.original_rect.width * width_ratio)
        self.rect.height = int(self.original_rect.height * height_ratio)
        
    def draw(self, screen):
        # Draw tab buttons
        tab_width = self.rect.width / len(self.tabs)
        for i, tab in enumerate(self.tabs):
            tab_rect = pygame.Rect(self.rect.x + i * tab_width, self.rect.y, tab_width, 30)
            color = PANEL_BG if i == self.active_tab else BUTTON_BG
            pygame.draw.rect(screen, color, tab_rect, border_radius=4)
            pygame.draw.rect(screen, (180, 180, 180), tab_rect, 1, border_radius=4)
            
            # Use smaller font if text is too long
            text_font = font_medium
            text_width = text_font.size(tab)[0]
            if text_width > tab_width - 10:
                text_font = font_small
                
            text_surf = text_font.render(tab, True, TEXT_COLOR)
            text_rect = text_surf.get_rect(center=tab_rect.center)
            screen.blit(text_surf, text_rect)
            
        # Draw content area
        content_rect = pygame.Rect(self.rect.x, self.rect.y + 30, self.rect.width, self.rect.height - 30)
        pygame.draw.rect(screen, PANEL_BG, content_rect, border_radius=4)
        pygame.draw.rect(screen, (180, 180, 180), content_rect, 1, border_radius=4)
        
        return content_rect
        
    def check_click(self, pos, event):
        if event.type == MOUSEBUTTONDOWN and event.button == 1:
            tab_width = self.rect.width / len(self.tabs)
            for i in range(len(self.tabs)):
                tab_rect = pygame.Rect(self.rect.x + i * tab_width, self.rect.y, tab_width, 30)
                if tab_rect.collidepoint(pos):
                    self.active_tab = i
                    return True
        return False

# Main application
class Viewer3D:
    def __init__(self):
        self.mesh = Mesh()
        self.camera = Camera()
        self.light_angle = 0.0
        self.running = True
        self.shading_mode = 0  # 0: Solid, 1: Wireframe, 2: Texture
        self.show_stats = True
        self.show_grid = True
        self.show_axes = True
        self.dragging = False
        self.panning = False
        self.last_mouse_pos = (0, 0)
        self.texture_map_mode = 0  # 0: Color, 1: Normal, 2: Specular, etc.
        self.texture_view_active = False  # Controls visibility of texture map buttons
        self.drag_and_drop_file = None
        self.performance_mode = False
        self.width_ratio = 1.0
        self.height_ratio = 1.0
        
        # UI elements
        self.tab_view = TabView(WIDTH - 300, 10, 290, HEIGHT - 20, ["Env & Light", "Stats & Shading"])
        
        # Environment & Lighting tab controls
        self.light_control = LightControl(WIDTH - 150, 150, 80)
        
        # Stats and Shading tab controls
        self.shading_buttons = [
            Button(WIDTH - 280, 100, 240, 40, "Solid Shading"),
            Button(WIDTH - 280, 150, 240, 40, "Wireframe View"),
            Button(WIDTH - 280, 200, 240, 40, "Texture View")
        ]
        self.shading_buttons[0].active = True
        
        # Texture map buttons (initially disabled)
        self.texture_buttons = [
            Button(WIDTH - 280, 260, 115, 40, "Color Map"),
            Button(WIDTH - 155, 260, 115, 40, "Normal Map"),
            Button(WIDTH - 280, 310, 115, 40, "Specular Map"),
            Button(WIDTH - 155, 310, 115, 40, "Metallic Map"),
            Button(WIDTH - 280, 360, 115, 40, "Roughness Map"),
            Button(WIDTH - 155, 360, 115, 40, "Glossiness Map")
        ]
        self.texture_buttons[0].active = True
        
        # Disable texture buttons initially
        for button in self.texture_buttons:
            button.enabled = False
        
        self.grid_button = Button(WIDTH - 280, 410, 115, 40, "Show Grid", None)
        self.grid_button.active = self.show_grid
        
        self.axes_button = Button(WIDTH - 155, 410, 115, 40, "Show Axes", None)
        self.axes_button.active = self.show_axes
        
        self.performance_button = Button(WIDTH - 280, 460, 240, 40, "Performance Mode", None)
        self.performance_button.active = self.performance_mode
        
        # Model buttons - fixed position to avoid going off screen
        self.model_buttons = [
            Button(20, HEIGHT - 120, 160, 40, "Cube Model"),
            Button(20, HEIGHT - 70, 160, 40, "Sphere Model"),
            Button(20, HEIGHT - 40, 160, 40, "Load OBJ...")  # Fixed position
        ]
        self.model_buttons[0].active = True
        
    def update_ui_positions(self):
        # Update all UI elements based on current window size
        self.width_ratio = WIDTH / 1200.0
        self.height_ratio = HEIGHT / 800.0
        
        self.tab_view.update_position(self.width_ratio, self.height_ratio)
        self.light_control.update_position(self.width_ratio, self.height_ratio)
        
        for button in (self.shading_buttons + self.texture_buttons + 
                      [self.grid_button, self.axes_button, self.performance_button] + self.model_buttons):
            button.update_position(self.width_ratio, self.height_ratio)
        
    def handle_events(self):
        mouse_pos = pygame.mouse.get_pos()
        
        for event in pygame.event.get():
            if event.type == QUIT:
                self.running = False
                
            # Handle window resize
            if event.type == VIDEORESIZE:
                global WIDTH, HEIGHT
                WIDTH = max(MIN_WIDTH, event.w)
                HEIGHT = max(MIN_HEIGHT, event.h)
                screen = pygame.display.set_mode((WIDTH, HEIGHT), pygame.RESIZABLE)
                self.update_ui_positions()
                self.camera.moved = True  # Force redraw
                
            # Handle drag and drop
            if event.type == DROPFILE:
                self.drag_and_drop_file = event.file
                if self.drag_and_drop_file.lower().endswith('.obj'):
                    if self.mesh.load_from_obj(self.drag_and_drop_file):
                        self.camera.auto_position(self.mesh)
                        # Update active model button
                        for button in self.model_buttons:
                            button.active = False
                
            # Handle tab view clicks
            self.tab_view.check_click(mouse_pos, event)
            
            # Handle light control
            self.light_control.check_click(mouse_pos, event)
            
            # Handle shading buttons
            for i, button in enumerate(self.shading_buttons):
                if button.check_click(mouse_pos, event):
                    for j, btn in enumerate(self.shading_buttons):
                        btn.active = (i == j)
                    self.shading_mode = i
                    
                    # Enable/disable texture buttons based on Texture View selection
                    self.texture_view_active = (i == 2)
                    for tex_button in self.texture_buttons:
                        tex_button.enabled = self.texture_view_active
                    
            # Handle texture map buttons only if texture view is active
            if self.texture_view_active:
                for i, button in enumerate(self.texture_buttons):
                    if button.check_click(mouse_pos, event):
                        for j, btn in enumerate(self.texture_buttons):
                            btn.active = (i == j)
                        self.texture_map_mode = i
                    
            # Handle grid button
            if self.grid_button.check_click(mouse_pos, event):
                self.show_grid = self.grid_button.active
                
            # Handle axes button
            if self.axes_button.check_click(mouse_pos, event):
                self.show_axes = self.axes_button.active
                
            # Handle performance button
            if self.performance_button.check_click(mouse_pos, event):
                self.performance_mode = self.performance_button.active
                
            # Handle model buttons
            for i, button in enumerate(self.model_buttons):
                if button.check_click(mouse_pos, event):
                    for j, btn in enumerate(self.model_buttons):
                        btn.active = (i == j)
                    if i == 0:
                        self.mesh.generate_cube()
                        self.camera.auto_position(self.mesh)
                    elif i == 1:
                        self.mesh.generate_sphere()
                        self.camera.auto_position(self.mesh)
                    elif i == 2:
                        # Load OBJ file
                        try:
                            import tkinter as tk
                            from tkinter import filedialog
                            root = tk.Tk()
                            root.withdraw()
                            file_path = filedialog.askopenfilename(
                                title="Select OBJ file",
                                filetypes=[("OBJ files", "*.obj"), ("All files", "*.*")]
                            )
                            if file_path:
                                if self.mesh.load_from_obj(file_path):
                                    self.camera.auto_position(self.mesh)
                        except:
                            print("Failed to open file dialog")
            
            # Handle camera rotation with mouse drag
            if event.type == MOUSEBUTTONDOWN:
                if event.button == 1:  # Left click for rotation
                    if not any([self.tab_view.rect.collidepoint(mouse_pos),
                               self.light_control.rect.collidepoint(mouse_pos)]):
                        for button in (self.shading_buttons + self.texture_buttons + 
                                      [self.grid_button, self.axes_button, self.performance_button] + self.model_buttons):
                            if button.rect.collidepoint(mouse_pos):
                                break
                        else:
                            self.dragging = True
                            self.last_mouse_pos = mouse_pos
                
                if event.button == 3:  # Right click for panning
                    if not any([self.tab_view.rect.collidepoint(mouse_pos),
                               self.light_control.rect.collidepoint(mouse_pos)]):
                        for button in (self.shading_buttons + self.texture_buttons + 
                                      [self.grid_button, self.axes_button, self.performance_button] + self.model_buttons):
                            if button.rect.collidepoint(mouse_pos):
                                break
                        else:
                            self.panning = True
                            self.last_mouse_pos = mouse_pos
                            
            if event.type == MOUSEBUTTONUP:
                if event.button == 1:
                    self.dragging = False
                if event.button == 3:
                    self.panning = False
                        
            if event.type == MOUSEMOTION:
                if self.dragging:
                    dx = mouse_pos[0] - self.last_mouse_pos[0]
                    dy = mouse_pos[1] - self.last_mouse_pos[1]
                    
                    # Fixed rotation - no twisting
                    self.camera.rot_y += dx * 0.01
                    self.camera.rot_x += dy * 0.01
                    
                    # Clamp vertical rotation to prevent flipping
                    self.camera.rot_x = max(-math.pi/2, min(math.pi/2, self.camera.rot_x))
                    
                    self.last_mouse_pos = mouse_pos
                    self.camera.moved = True
                
                if self.panning:
                    dx = mouse_pos[0] - self.last_mouse_pos[0]
                    dy = mouse_pos[1] - self.last_mouse_pos[1]
                    
                    # Inverted panning with half sensitivity as requested
                    self.camera.pan_x -= dx * 0.005  # Inverted and half sensitivity
                    self.camera.pan_y += dy * 0.005  # Inverted and half sensitivity
                    
                    self.last_mouse_pos = mouse_pos
                    self.camera.moved = True
                        
            # Handle zoom with mouse wheel
            if event.type == MOUSEWHEEL:
                self.camera.z += event.y * 0.5
                self.camera.moved = True
        
        # Update button hover states
        for button in (self.shading_buttons + self.texture_buttons + 
                      [self.grid_button, self.axes_button, self.performance_button] + self.model_buttons):
            button.check_hover(mouse_pos)
            
    def update(self):
        # Update light rotation based on control
        self.light_angle = self.light_control.angle
        
    def draw(self):
        screen.fill(BACKGROUND)
        
        # Draw 3D grid
        if self.show_grid:
            self.draw_grid()
            
        # Draw coordinate axes
        if self.show_axes:
            self.draw_axes()
        
        # Draw the 3D object with depth sorting
        self.draw_mesh()
        
        # Draw UI panels
        self.draw_ui()
        
        # Draw drag and drop notification
        if self.drag_and_drop_file:
            text = font_medium.render(f"Loaded: {os.path.basename(self.drag_and_drop_file)}", True, TEXT_COLOR)
            screen.blit(text, (20, 20))
        
        pygame.display.flip()
        
    def draw_grid(self):
        grid_size = 10
        grid_step = 1
        half_size = grid_size / 2
        
        for i in range(-grid_size, grid_size + 1, grid_step):
            # X lines
            p1 = self.camera.project((i, -half_size, 0))
            p2 = self.camera.project((i, half_size, 0))
            if p1[2] > 0 and p2[2] > 0:
                pygame.draw.line(screen, GRID_COLOR, (p1[0], p1[1]), (p2[0], p2[1]), 1)
                
            # Y lines
            p1 = self.camera.project((-half_size, i, 0))
            p2 = self.camera.project((half_size, i, 0))
            if p1[2] > 0 and p2[2] > 0:
                pygame.draw.line(screen, GRID_COLOR, (p1[0], p1[1]), (p2[0], p2[1]), 1)
                
    def draw_axes(self):
        # X axis (red)
        p1 = self.camera.project((0, 0, 0))
        p2 = self.camera.project((2, 0, 0))
        if p1[2] > 0 and p2[2] > 0:
            pygame.draw.line(screen, AXIS_X, (p1[0], p1[1]), (p2[0], p2[1]), 3)
            text = font_small.render("X", True, AXIS_X)
            screen.blit(text, (p2[0] + 5, p2[1]))
            
        # Y axis (green)
        p2 = self.camera.project((0, 2, 0))
        if p1[2] > 0 and p2[2] > 0:
            pygame.draw.line(screen, AXIS_Y, (p1[0], p1[1]), (p2[0], p2[1]), 3)
            text = font_small.render("Y", True, AXIS_Y)
            screen.blit(text, (p2[0] + 5, p2[1]))
            
        # Z axis (blue)
        p2 = self.camera.project((0, 0, 2))
        if p1[2] > 0 and p2[2] > 0:
            pygame.draw.line(screen, AXIS_Z, (p1[0], p1[1]), (p2[0], p2[1]), 3)
            text = font_small.render("Z", True, AXIS_Z)
            screen.blit(text, (p2[0] + 5, p2[1]))
        
    def draw_mesh(self):
        # Calculate light direction from angle
        light_dir = (
            math.cos(self.light_angle),
            math.sin(self.light_angle),
            0.5  # Fixed height for the light
        )
        
        # Normalize light direction
        length = math.sqrt(light_dir[0]**2 + light_dir[1]**2 + light_dir[2]**2)
        if length > 0:
            light_dir = (light_dir[0]/length, light_dir[1]/length, light_dir[2]/length)
        
        # Check if we can use cached projections
        current_key = (self.camera.rot_x, self.camera.rot_y, self.camera.z, self.camera.pan_x, self.camera.pan_y)
        use_cache = not self.camera.moved and current_key == self.camera.cache_key and not self.performance_mode
        
        if not use_cache:
            self.mesh.vertex_cache = {}
            self.camera.cache_key = current_key
            self.camera.moved = False
        
        # Sort faces by depth for proper rendering (painter's algorithm)
        face_depths = []
        for i, face in enumerate(self.mesh.faces):
            # Calculate average z depth of face
            avg_z = 0
            valid_face = True
            for vertex_idx in face:
                if vertex_idx < len(self.mesh.vertices):
                    vertex = self.mesh.vertices[vertex_idx]
                    
                    # Use cached projection if available
                    if use_cache and vertex_idx in self.mesh.vertex_cache:
                        projected = self.mesh.vertex_cache[vertex_idx]
                    else:
                        projected = self.camera.project(vertex)
                        self.mesh.vertex_cache[vertex_idx] = projected
                    
                    avg_z += projected[2]
                else:
                    valid_face = False
                    break
                    
            if valid_face and len(face) > 0:
                avg_z /= len(face)
                face_depths.append((i, avg_z))
        
        # Sort by depth (farthest first)
        face_depths.sort(key=lambda x: x[1], reverse=True)
        
        # Draw each face in depth order
        for face_idx, _ in face_depths:
            if face_idx >= len(self.mesh.faces):
                continue
                
            face = self.mesh.faces[face_idx]
            
            if face_idx < len(self.mesh.normals):
                normal = self.mesh.normals[face_idx]
            else:
                # Calculate normal if not provided
                if len(face) >= 3:
                    v1 = self.mesh.vertices[face[0]]
                    v2 = self.mesh.vertices[face[1]]
                    v3 = self.mesh.vertices[face[2]]
                    
                    u = (v2[0] - v1[0], v2[1] - v1[1], v2[2] - v1[2])
                    v = (v3[0] - v1[0], v3[1] - v1[1], v3[2] - v1[2])
                    
                    normal = (
                        u[1] * v[2] - u[2] * v[1],
                        u[2] * v[0] - u[0] * v[2],
                        u[0] * v[1] - u[1] * v[0]
                    )
                    
                    # Normalize
                    length = math.sqrt(normal[0]**2 + normal[1]**2 + normal[2]**2)
                    if length > 0:
                        normal = (normal[0]/length, normal[1]/length, normal[2]/length)
                    else:
                        normal = (0, 0, 1)
                else:
                    normal = (0, 0, 1)
            
            # Calculate lighting - only if face is facing the camera
            # Use the camera's forward vector (0, 0, 1) in view space
            facing = normal[2] > 0  # Simplified facing check
            
            if facing:
                intensity = max(0.2, normal[0] * light_dir[0] + normal[1] * light_dir[1] + normal[2] * light_dir[2])
            else:
                intensity = 0.2  # Minimal ambient light for backfaces
            
            # Get face color based on texture map mode
            if face_idx < len(self.mesh.colors):
                base_color = self.mesh.colors[face_idx]
            else:
                base_color = (200, 200, 200)
                
            # Apply texture map mode
            if self.texture_map_mode == 0:  # Color Map
                color = (
                    int(base_color[0] * intensity),
                    int(base_color[1] * intensity),
                    int(base_color[2] * intensity)
                )
            elif self.texture_map_mode == 1:  # Normal Map
                # Blue-ish tint for normal maps
                color = (
                    int(100 * intensity),
                    int(100 * intensity),
                    int(255 * intensity)
                )
            elif self.texture_map_mode == 2:  # Specular Map
                # White with specular highlights
                color = (
                    int(255 * intensity),
                    int(255 * intensity),
                    int(255 * intensity)
                )
            elif self.texture_map_mode == 3:  # Metallic Map
                # Gray metallic look
                metallic = (intensity + 0.5) / 2
                color = (
                    int(200 * metallic),
                    int(200 * metallic),
                    int(200 * metallic)
                )
            elif self.texture_map_mode == 4:  # Roughness Map
                # Darker, less reflective
                roughness = 1 - intensity
                color = (
                    int(100 * roughness),
                    int(100 * roughness),
                    int(100 * roughness)
                )
            else:  # Glossiness Map
                # Bright, reflective
                glossiness = intensity * 1.5
                color = (
                    int(255 * glossiness),
                    int(255 * glossiness),
                    int(255 * glossiness)
                )
            
            # Project vertices
            vertices_2d = []
            valid_face = True
            for vertex_idx in face:
                if vertex_idx < len(self.mesh.vertices):
                    # Use cached projection if available
                    if vertex_idx in self.mesh.vertex_cache:
                        projected = self.mesh.vertex_cache[vertex_idx]
                    else:
                        vertex = self.mesh.vertices[vertex_idx]
                        projected = self.camera.project(vertex)
                        self.mesh.vertex_cache[vertex_idx] = projected
                    
                    vertices_2d.append((projected[0], projected[1]))
                else:
                    valid_face = False
                    break
                
            # Only draw if the face is facing the camera and has valid vertices
            if facing and valid_face and len(vertices_2d) >= 3:
                if self.shading_mode == 0:  # Solid shading
                    pygame.draw.polygon(screen, color, vertices_2d)
                    if not self.performance_mode:
                        pygame.draw.polygon(screen, (180, 180, 180), vertices_2d, 1)
                elif self.shading_mode == 1:  # Wireframe
                    pygame.draw.polygon(screen, BACKGROUND, vertices_2d)
                    pygame.draw.polygon(screen, color, vertices_2d, 1)
                elif self.shading_mode == 2:  # Texture (simulated with solid color)
                    pygame.draw.polygon(screen, color, vertices_2d)
                    if not self.performance_mode:
                        pygame.draw.polygon(screen, (150, 150, 150), vertices_2d, 1)
        
    def draw_ui(self):
        # Draw tab view
        content_rect = self.tab_view.draw(screen)
        
        # Draw content based on active tab
        if self.tab_view.active_tab == 0:  # Environment & Lighting
            # Draw light control
            self.light_control.draw(screen)
            
        else:  # Stats and Shading
            for button in self.shading_buttons:
                button.draw(screen)
                
            # Draw texture map buttons in a grouped box if texture view is active
            if self.texture_view_active:
                # Draw background box for texture buttons
                texture_box = pygame.Rect(WIDTH - 290, 250, 270, 160)
                pygame.draw.rect(screen, BUTTON_BG, texture_box, border_radius=8)
                pygame.draw.rect(screen, (180, 180, 180), texture_box, 1, border_radius=8)
                
                # Draw texture map buttons
                for button in self.texture_buttons:
                    button.draw(screen)
            else:
                # Draw disabled texture buttons
                for button in self.texture_buttons:
                    button.draw(screen)
                
            self.grid_button.draw(screen)
            self.axes_button.draw(screen)
            self.performance_button.draw(screen)
            
            # Always show stats
            stats_bg = pygame.Rect(WIDTH - 280, 510, 240, 150)
            pygame.draw.rect(screen, BUTTON_BG, stats_bg, border_radius=4)
            pygame.draw.rect(screen, (180, 180, 180), stats_bg, 1, border_radius=4)
            
            # Draw stats text
            title = font_medium.render("Model Statistics", True, TEXT_COLOR)
            screen.blit(title, (stats_bg.x + 10, stats_bg.y + 10))
            
            vertices = font_small.render(f"Vertices: {len(self.mesh.vertices)}", True, TEXT_COLOR)
            screen.blit(vertices, (stats_bg.x + 20, stats_bg.y + 50))
            
            triangles = font_small.render(f"Triangles: {len(self.mesh.faces)}", True, TEXT_COLOR)
            screen.blit(triangles, (stats_bg.x + 20, stats_bg.y + 80))
            
            edges = font_small.render(f"Edges: {len(self.mesh.faces) * 3 // 2}", True, TEXT_COLOR)
            screen.blit(edges, (stats_bg.x + 20, stats_bg.y + 110))
        
        # Draw model buttons
        for button in self.model_buttons:
            button.draw(screen)
        
    def run(self):
        clock = pygame.time.Clock()
        
        # Enable drag and drop
        pygame.event.set_allowed([DROPFILE])
        
        while self.running:
            self.handle_events()
            self.update()
            self.draw()
            clock.tick(60)

# Run the application
if __name__ == "__main__":
    viewer = Viewer3D()
    viewer.run()
    pygame.quit()
