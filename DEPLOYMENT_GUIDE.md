# 🚀 EduVi Backend Server & Master Deployment Guide

This guide details the deployment pipeline for the **EduVi Platform**. The `.NET Backend (SEP490_BE)` and the `Python AI Microservices (SEP490_AI)` operate as a unified system on a single Google Cloud Platform (GCP) Virtual Machine. 

**This `.NET` repository acts as the master deployment repository.** It contains the production `docker-compose.yml` that orchestrates all backend components, databases, and AI workers.

---

## 🏗️ 1. Unified Architecture Overview

1. Code pushed to `main` triggers GitHub Actions.
2. GitHub Actions builds the Docker image and pushes it to GitHub Container Registry (GHCR).
3. The Action connects to the GCP VM via SSH.
4. The VM executes a `docker compose pull` and restarts the updated containers seamlessly.

---

## ⚙️ 2. Repository CI/CD Setup (.NET Backend)

The backend is containerized using a multi-stage `Dockerfile` (`sdk:8.0` for building, `aspnet:8.0` for runtime) located in the repository root.

### GitHub Actions Workflow
The workflow file is located at `.github/workflows/deploy.yml`. 
* **Trigger:** Push to `main`.
* **Process:** Builds the image, tags it with `:latest` and the exact `:sha`, pushes to `ghcr.io/sep490-eduvi/main-system`, and signals the VM to pull & restart.

### GitHub Secrets Required
To enable automated deployments, go to **Settings > Secrets and variables > Actions** in the GitHub repository and ensure these are set:

| Secret Name | Value |
|---|---|
| `SERVER_HOST` | The external IP of the GCP VM (e.g., `34.87.16.235`) |
| `SERVER_USER` | The SSH username on the VM (e.g., `minhquang932004`) |
| `SERVER_SSH_KEY` | The raw private SSH key content used to authenticate |

*(Note: `GITHUB_TOKEN` is built-in to Actions, so no separate GHCR token secret is required).*

---

## 🖥️ 3. GCP VM Initialization & Server Setup

If starting from scratch, here is exactly how the VM is prepared and authorized.

### Step 3.1: Create the Virtual Machine & ADC
1. **Machine:** `e2-standard-2` (2 vCPU, 8 GB RAM) on Ubuntu 22.04 LTS.
2. **Service Account:** Attach a GCP Service Account with `Storage Object Viewer` and `Vertex AI User` roles. This provides Application Default Credentials (ADC) to Docker automatically, eliminating the need for `gcp-key.json`!
3. **Firewall:** Ensure **Allow HTTP traffic** and **Allow HTTPS traffic** are both checked. (Ports like 5672 or 15672 for RabbitMQ remain blocked from the external internet by default.)

### Step 3.2: Install Docker & Authenticate GHCR
SSH into the fresh VM and run:
```bash
# Install Docker
sudo apt update
sudo apt install -y docker.io docker-compose-v2
sudo usermod -aG docker $USER

# Authenticate with GitHub Container Registry (Using your classic GitHub PAT)
echo "YOUR_GITHUB_PAT" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

### Step 3.3: Create `.env` & First Deploy
On the VM, create the directory and config:
```bash
sudo mkdir -p /opt/eduvi
sudo nano /opt/eduvi/.env
```
Populate It (ensuring `GITHUB_ORG` is lowercase):
```env
GITHUB_ORG=sep490-eduvi
GOOGLE_CLOUD_PROJECT=fit-boulevard-479205-h1
# ... [Add remaining variables from your env reference] ...
```
Then copy the `docker-compose.yml` to the folder and run:
```bash
cd /opt/eduvi
docker compose pull
docker compose up -d
```

---

## 🌐 4. Web Server & SSL (Nginx + Certbot)

The server uses Nginx as a reverse proxy, mapping port `80` and `443` to the internal `main-system:8080`. SSL Certificates are provisioned directly on the host using Certbot.

### Generating the SSL Cert (On the Server):
```bash
# Install certbot on host
sudo apt update && sudo apt install -y certbot

# Temporarily stop Nginx to free port 80
cd /opt/eduvi
docker compose stop nginx

# Run standalone challenge
sudo certbot certonly --standalone -d api.eduvi.tech --non-interactive --agree-tos -m <your_email>

# Restart Nginx
docker compose up -d nginx
```

---

## 🛠️ 5. Server Master Configuration (Via VS Code)

Once the server is initialized, you manage maintenance using **VS Code Remote - SSH**.

1. Install the **"Remote - SSH"** extension in VS Code.
2. Open the command palette (`Ctrl+Shift+P`) -> **Remote-SSH: Open SSH Configuration File**.
3. Add the server block:
   ```text
   Host eduvi-server
     HostName 34.87.16.235
     User minhquang932004
     IdentityFile C:\Users\<your_username>\.ssh\eduvi_deploy
   ```
4. Connect to `eduvi-server` and Open Folder: `/opt/eduvi/`
5. Here you can edit the global `.env` and the master `docker-compose.yml`.
6. After changing `.env` variables, apply them by running:
   ```bash
   docker compose down
   docker compose up -d
   ```

---

## 🛡️ 6. Accessing Secure Internal Services (SSH Tunnel)

To protect the system, internal services like **RabbitMQ**, **Redis**, and **Dozzle** are NOT exposed to the public internet. Teammates must use an SSH tunnel to access them.

### Step-by-Step Tunnel Setup for Teammates:
1. Place the shared private key (`eduvi_deploy`) into `C:\Users\<username>\.ssh\`.
2. Fix permissions in PowerShell:
   ```powershell
   icacls "$env:USERPROFILE\.ssh\eduvi_deploy" /inheritance:r /grant:r "$env:USERNAME:R"
   ```
3. Add to `C:\Users\<username>\.ssh\config`:
   ```text
   Host eduvi-tunnel
     HostName 34.87.16.235
     User minhquang932004
     IdentityFile C:\Users\<your_username>\.ssh\eduvi_deploy
     LocalForward 9999 127.0.0.1:9999
     LocalForward 8081 127.0.0.1:8081
     LocalForward 15672 127.0.0.1:15672
   ```
4. Open the tunnel securely by running this in an open PowerShell window:
   ```powershell
   ssh -N eduvi-tunnel
   ```
5. Access via Browser:
   * **Dozzle System Logs:** [http://127.0.0.1:9999](http://127.0.0.1:9999)
   * **RabbitMQ Dashboard:** [http://127.0.0.1:15672](http://127.0.0.1:15672) 
   * **Internal Swagger:** [http://127.0.0.1:8081/swagger/index.html](http://127.0.0.1:8081/swagger/index.html)

---

## 🚑 7. Troubleshooting

### Force Updating RabbitMQ Credentials
Occasionally, Docker fails to apply `.env` credentials to a persistently running RabbitMQ volume. If you cannot log in to RabbitMQ using the credentials in `.env`, run these CLI commands directly on the server to **force set** the password:
```bash
# Change password for your user manually inside the container
docker exec -it rabbitmq rabbitmqctl change_password eduvi minhquang123
docker exec -it rabbitmq rabbitmqctl authenticate_user eduvi minhquang123
```