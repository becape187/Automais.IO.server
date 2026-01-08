using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class latencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adicionar coluna Latency se ela não existir
            // Usando SQL raw para verificar se a coluna já existe antes de adicionar
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'public' 
                        AND table_name = 'routers' 
                        AND column_name = 'Latency'
                    ) THEN
                        ALTER TABLE routers ADD COLUMN ""Latency"" integer NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remover coluna Latency se ela existir
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'public' 
                        AND table_name = 'routers' 
                        AND column_name = 'Latency'
                    ) THEN
                        ALTER TABLE routers DROP COLUMN ""Latency"";
                    END IF;
                END $$;
            ");
        }
    }
}
