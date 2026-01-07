#!/usr/bin/env python3
"""
Script para testar conexão com RouterOS API
Uso: python3 test_routeros_api.py <host> <port> <username> <password>
Exemplo: python3 test_routeros_api.py 10.222.111.2 8728 automais senha123
"""

import sys
import socket
import struct

def write_word(sock, word):
    """Escreve uma palavra no formato RouterOS API"""
    word_bytes = word.encode('utf-8')
    length = len(word_bytes)
    # Enviar comprimento (4 bytes, little-endian)
    sock.send(struct.pack('<I', length))
    # Enviar palavra
    if length > 0:
        sock.send(word_bytes)

def read_word(sock):
    """Lê uma palavra no formato RouterOS API"""
    # Ler comprimento (4 bytes, little-endian)
    length_bytes = sock.recv(4)
    if len(length_bytes) < 4:
        return None
    length = struct.unpack('<I', length_bytes)[0]
    
    if length == 0:
        return ""
    
    if length > 8192:
        print(f"ERRO: Palavra muito grande: {length} bytes")
        return None
    
    # Ler palavra
    word_bytes = sock.recv(length)
    if len(word_bytes) < length:
        return None
    
    return word_bytes.decode('utf-8')

def test_routeros_api(host, port, username, password):
    """Testa conexão com RouterOS API"""
    try:
        print(f"Conectando ao {host}:{port}...")
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(5)
        sock.connect((host, port))
        print("✅ Conexão TCP estabelecida!")
        
        # Protocolo RouterOS API:
        # 1. Enviar palavra vazia para iniciar
        print("Enviando palavra vazia...")
        write_word(sock, "")
        
        # 2. Ler resposta inicial
        print("Lendo resposta inicial...")
        initial_response = read_word(sock)
        print(f"Resposta inicial: {initial_response}")
        
        # 3. Enviar comando de login
        print("Enviando comando de login...")
        write_word(sock, "/login")
        write_word(sock, f"=name={username}")
        write_word(sock, f"=password={password}")
        write_word(sock, "")  # Finalizar comando
        
        # 4. Ler respostas
        print("Lendo respostas de login...")
        responses = []
        for i in range(10):
            response = read_word(sock)
            if response is None:
                break
            responses.append(response)
            print(f"  Resposta [{i}]: {response}")
            
            if response.startswith("!done"):
                print("✅ Login bem-sucedido!")
                sock.close()
                return True
            elif response.startswith("!trap"):
                print("❌ Login falhou (trap)")
                sock.close()
                return False
        
        print(f"⚠️  Respostas recebidas: {responses}")
        sock.close()
        return False
        
    except socket.timeout:
        print("❌ Timeout ao conectar")
        return False
    except ConnectionRefusedError:
        print("❌ Conexão recusada")
        return False
    except Exception as e:
        print(f"❌ Erro: {e}")
        return False

if __name__ == "__main__":
    if len(sys.argv) != 5:
        print("Uso: python3 test_routeros_api.py <host> <port> <username> <password>")
        print("Exemplo: python3 test_routeros_api.py 10.222.111.2 8728 automais senha123")
        sys.exit(1)
    
    host = sys.argv[1]
    port = int(sys.argv[2])
    username = sys.argv[3]
    password = sys.argv[4]
    
    success = test_routeros_api(host, port, username, password)
    sys.exit(0 if success else 1)

